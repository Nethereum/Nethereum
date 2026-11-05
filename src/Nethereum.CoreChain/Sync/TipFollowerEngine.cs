using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Sync
{
    internal sealed class TipFollowerEngine : FollowerEngineBase
    {
        private readonly BackwardWalkerDelegate _walker;
        private readonly AncestorResolverDelegate _ancestorResolver;
        private readonly BodyRepairDelegate _bodyRepair;
        private static readonly Nethereum.Model.IBlockRootsProvider _bodyRootsProvider = new PatriciaBlockRootsProvider();

        private const int MaxDivergenceRepairAttempts = 3;

        public TipFollowerEngine(
            BackwardWalkerDelegate walker,
            AncestorResolverDelegate ancestorResolver,
            BodyRepairDelegate bodyRepair)
        {
            _walker = walker;
            _ancestorResolver = ancestorResolver;
            _bodyRepair = bodyRepair;
        }

        internal async Task<FollowerRunResult> RunAsync(
            IChainStoreBundle bundle,
            IBlockExecutor executor,
            Func<IChainStoreBundle, IBlockExecutor> executorFactory,
            ICanonicalStateRootSource canonical,
            FollowerOptions options,
            ulong lastCommittedBlock,
            byte[] lastCommittedHash,
            CancellationToken ct,
            ILogger logger)
        {
            var pollInterval = options.TipPollInterval ?? TimeSpan.FromSeconds(12);
            ulong blocksExecuted = 0UL;
            ulong rootMismatches = 0UL;
            ulong lastSeenTipBlock = 0UL;
            byte[] lastSeenTipHash = null;
            int consecutiveSourceFailures = 0;
            ulong consecutiveDivergences = 0UL;
            ulong rewindCyclesUsed = 0UL;
            int consecutiveBodyRepairs = 0;

            options.ChainWriteGate?.Publish(executor);
            options.ChainWriteGate?.SetHead(lastCommittedBlock, lastCommittedHash);
            SeedHeaderSubchainFromFrontier(bundle, options, lastCommittedBlock);
            await ReorgRewindCoordinator.SeedExecutedHashesAsync(
                bundle, lastCommittedBlock, lastCommittedHash,
                System.Math.Min(options.MaxReorgDepth, ExecutedHashRetention.Blocks)).ConfigureAwait(false);

            try
            {
                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    AdoptGateHead(options.ChainWriteGate, ref lastCommittedBlock, ref lastCommittedHash, ref blocksExecuted);

                    var acquired = await AcquireActionableTipAsync(
                        canonical, options, pollInterval,
                        consecutiveSourceFailures, lastCommittedBlock, lastSeenTipBlock, lastSeenTipHash,
                        blocksExecuted, rootMismatches, ct, logger).ConfigureAwait(false);
                    consecutiveSourceFailures = acquired.ConsecutiveSourceFailures;
                    lastSeenTipBlock = acquired.LastSeenTipBlock;
                    lastSeenTipHash = acquired.LastSeenTipHash;
                    if (acquired.Terminal is FollowerRunResult acquiredTerminal) return acquiredTerminal;
                    if (acquired.Signal == TipCycleSignal.Continue) continue;
                    var tip = acquired.Tip;

                    ulong cursor = lastCommittedBlock;
                    ulong delta = tip.BlockNumber - cursor;
                    if (BelowWalkerInvocationThreshold(delta, options))
                    {
                        await Task.Delay(pollInterval, ct).ConfigureAwait(false);
                        continue;
                    }

                    logger.LogInformation(
                        "snap.cycle.restart reason=\"walker_invoked, delta={Delta}\"",
                        delta);

                    var advanced = await AdvanceHeaderSubchainToTipAsync(
                        tip, cursor, bundle, executor, options, pollInterval,
                        lastCommittedBlock, lastCommittedHash, lastSeenTipBlock, lastSeenTipHash,
                        blocksExecuted, rootMismatches, ct, logger).ConfigureAwait(false);
                    lastCommittedBlock = advanced.LastCommittedBlock;
                    lastCommittedHash = advanced.LastCommittedHash;
                    lastSeenTipBlock = advanced.LastSeenTipBlock;
                    lastSeenTipHash = advanced.LastSeenTipHash;
                    if (advanced.Terminal is FollowerRunResult advancedTerminal) return advancedTerminal;
                    if (advanced.Signal == TipCycleSignal.Continue) continue;

                    var executed = await ExecuteConfirmedRangeAsync(
                        tip, cursor, bundle, executor, executorFactory, options,
                        lastCommittedBlock, lastCommittedHash, lastSeenTipBlock, lastSeenTipHash,
                        blocksExecuted, rootMismatches, consecutiveDivergences, rewindCyclesUsed,
                        consecutiveBodyRepairs, ct, logger).ConfigureAwait(false);
                    lastCommittedBlock = executed.LastCommittedBlock;
                    lastCommittedHash = executed.LastCommittedHash;
                    lastSeenTipBlock = executed.LastSeenTipBlock;
                    lastSeenTipHash = executed.LastSeenTipHash;
                    blocksExecuted = executed.BlocksExecuted;
                    rootMismatches = executed.RootMismatches;
                    consecutiveDivergences = executed.ConsecutiveDivergences;
                    rewindCyclesUsed = executed.RewindCyclesUsed;
                    consecutiveBodyRepairs = executed.ConsecutiveBodyRepairs;
                    executor = executed.Executor;
                    if (executed.Terminal is FollowerRunResult executedTerminal) return executedTerminal;
                    if (executed.BodyHoleAt is ulong holeBlock)
                    {
                        var bodyRepair = await RepairBodyHoleAsync(
                            holeBlock, tip.BlockNumber, bundle, options, consecutiveBodyRepairs,
                            lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                            ct, logger).ConfigureAwait(false);
                        if (bodyRepair.Terminal is FollowerRunResult bodyTerminal) return bodyTerminal;
                        consecutiveBodyRepairs = bodyRepair.NewRepairCount;
                        ForceTipRepollNextCycle(ref lastSeenTipBlock, ref lastSeenTipHash);
                        continue;
                    }
                    if (executed.Signal == TipCycleSignal.Continue) continue;

                    if (ReachedConfiguredEndBlock(lastCommittedBlock, options))
                    {
                        return new FollowerRunResult(
                            FollowerExitReason.SourceCompleted,
                            lastCommittedBlock, blocksExecuted, rootMismatches, RewindCyclesUsed: 0,
                            SnapshotRestoreTarget: null,
                            Detail: $"reached EndBlock={options.EndBlock.Value:N0}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                AdoptGateHead(options.ChainWriteGate, ref lastCommittedBlock, ref lastCommittedHash, ref blocksExecuted);
                return new FollowerRunResult(
                    FollowerExitReason.Cancelled,
                    lastCommittedBlock, blocksExecuted, rootMismatches, RewindCyclesUsed: 0,
                    SnapshotRestoreTarget: null,
                    Detail: "cancelled");
            }
            finally
            {
                await CommitCursorAtRunExitAsync(bundle, lastCommittedBlock, lastCommittedHash, logger)
                    .ConfigureAwait(false);
            }
        }

        private static void AdoptGateHead(
            AsyncGate gate, ref ulong lastCommittedBlock, ref byte[] lastCommittedHash, ref ulong blocksExecuted)
        {
            if (gate == null || !gate.HasHead) return;
            var head = gate.HeadNumber;
            if (head > lastCommittedBlock) blocksExecuted += head - lastCommittedBlock;
            lastCommittedBlock = head;
            lastCommittedHash = gate.HeadHash ?? lastCommittedHash;
        }

        private static async Task<T> UnderGateAsync<T>(FollowerOptions options, CancellationToken ct, Func<Task<T>> work)
        {
            var gate = options.ChainWriteGate;
            if (gate != null) await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                return await work().ConfigureAwait(false);
            }
            finally
            {
                gate?.Release();
            }
        }

        private enum TipCycleSignal { Proceed, Continue }

        private readonly struct TipAcquisition
        {
            public TipCycleSignal Signal { get; init; }
            public FollowerRunResult Terminal { get; init; }
            public CanonicalTip Tip { get; init; }
            public int ConsecutiveSourceFailures { get; init; }
            public ulong LastSeenTipBlock { get; init; }
            public byte[] LastSeenTipHash { get; init; }

            public static TipAcquisition Halted(
                FollowerRunResult terminal, int consecutiveSourceFailures, ulong lastSeenTipBlock, byte[] lastSeenTipHash)
                => new TipAcquisition
                {
                    Terminal = terminal,
                    ConsecutiveSourceFailures = consecutiveSourceFailures,
                    LastSeenTipBlock = lastSeenTipBlock,
                    LastSeenTipHash = lastSeenTipHash,
                };

            public static TipAcquisition Retry(
                int consecutiveSourceFailures, ulong lastSeenTipBlock, byte[] lastSeenTipHash)
                => new TipAcquisition
                {
                    Signal = TipCycleSignal.Continue,
                    ConsecutiveSourceFailures = consecutiveSourceFailures,
                    LastSeenTipBlock = lastSeenTipBlock,
                    LastSeenTipHash = lastSeenTipHash,
                };

            public static TipAcquisition Ready(CanonicalTip tip, int consecutiveSourceFailures)
                => new TipAcquisition
                {
                    Signal = TipCycleSignal.Proceed,
                    Tip = tip,
                    ConsecutiveSourceFailures = consecutiveSourceFailures,
                    LastSeenTipBlock = tip.BlockNumber,
                    LastSeenTipHash = tip.BlockHash,
                };
        }

        private async Task<TipAcquisition> AcquireActionableTipAsync(
            ICanonicalStateRootSource canonical,
            FollowerOptions options,
            TimeSpan pollInterval,
            int consecutiveSourceFailures,
            ulong lastCommittedBlock,
            ulong lastSeenTipBlock,
            byte[] lastSeenTipHash,
            ulong blocksExecuted,
            ulong rootMismatches,
            CancellationToken ct,
            ILogger logger)
        {
            var (tip, lastSourceException) =
                await PollCanonicalTipAsync(canonical, pollInterval, ct, logger).ConfigureAwait(false);

            if (tip == null)
            {
                var unreachable = SpendSourceFailureBudget(
                    ref consecutiveSourceFailures, options, canonical, lastSourceException,
                    lastCommittedBlock, blocksExecuted, rootMismatches, logger);
                if (unreachable != null)
                    return TipAcquisition.Halted(
                        unreachable, consecutiveSourceFailures, lastSeenTipBlock, lastSeenTipHash);
                await Task.Delay(pollInterval, ct).ConfigureAwait(false);
                return TipAcquisition.Retry(consecutiveSourceFailures, lastSeenTipBlock, lastSeenTipHash);
            }

            consecutiveSourceFailures = 0;

            if (TipOffersNothingNew(tip, lastCommittedBlock, lastSeenTipBlock, lastSeenTipHash))
            {
                await Task.Delay(pollInterval, ct).ConfigureAwait(false);
                return TipAcquisition.Retry(consecutiveSourceFailures, lastSeenTipBlock, lastSeenTipHash);
            }

            return TipAcquisition.Ready(tip, consecutiveSourceFailures);
        }

        private readonly struct HeaderSubchainAdvance
        {
            public TipCycleSignal Signal { get; init; }
            public FollowerRunResult Terminal { get; init; }
            public ulong LastCommittedBlock { get; init; }
            public byte[] LastCommittedHash { get; init; }
            public ulong LastSeenTipBlock { get; init; }
            public byte[] LastSeenTipHash { get; init; }

            public static HeaderSubchainAdvance Halted(
                FollowerRunResult terminal, ulong lastCommittedBlock, byte[] lastCommittedHash,
                ulong lastSeenTipBlock, byte[] lastSeenTipHash)
                => new HeaderSubchainAdvance
                {
                    Terminal = terminal,
                    LastCommittedBlock = lastCommittedBlock,
                    LastCommittedHash = lastCommittedHash,
                    LastSeenTipBlock = lastSeenTipBlock,
                    LastSeenTipHash = lastSeenTipHash,
                };

            public static HeaderSubchainAdvance Retry(
                ulong lastCommittedBlock, byte[] lastCommittedHash, ulong lastSeenTipBlock, byte[] lastSeenTipHash)
                => new HeaderSubchainAdvance
                {
                    Signal = TipCycleSignal.Continue,
                    LastCommittedBlock = lastCommittedBlock,
                    LastCommittedHash = lastCommittedHash,
                    LastSeenTipBlock = lastSeenTipBlock,
                    LastSeenTipHash = lastSeenTipHash,
                };

            public static HeaderSubchainAdvance Ready(
                ulong lastCommittedBlock, byte[] lastCommittedHash, ulong lastSeenTipBlock, byte[] lastSeenTipHash)
                => new HeaderSubchainAdvance
                {
                    Signal = TipCycleSignal.Proceed,
                    LastCommittedBlock = lastCommittedBlock,
                    LastCommittedHash = lastCommittedHash,
                    LastSeenTipBlock = lastSeenTipBlock,
                    LastSeenTipHash = lastSeenTipHash,
                };
        }

        private static HeaderSubchainAdvance AfterRepair(
            FollowerRunResult terminal, ulong lastCommittedBlock, byte[] lastCommittedHash,
            ulong lastSeenTipBlock, byte[] lastSeenTipHash)
        {
            if (terminal != null)
                return HeaderSubchainAdvance.Halted(
                    terminal, lastCommittedBlock, lastCommittedHash, lastSeenTipBlock, lastSeenTipHash);
            ForceTipRepollNextCycle(ref lastSeenTipBlock, ref lastSeenTipHash);
            return HeaderSubchainAdvance.Retry(lastCommittedBlock, lastCommittedHash, lastSeenTipBlock, lastSeenTipHash);
        }

        private async Task<HeaderSubchainAdvance> AdvanceHeaderSubchainToTipAsync(
            CanonicalTip tip,
            ulong cursor,
            IChainStoreBundle bundle,
            IBlockExecutor executor,
            FollowerOptions options,
            TimeSpan pollInterval,
            ulong lastCommittedBlock,
            byte[] lastCommittedHash,
            ulong lastSeenTipBlock,
            byte[] lastSeenTipHash,
            ulong blocksExecuted,
            ulong rootMismatches,
            CancellationToken ct,
            ILogger logger)
        {
            if (!options.ExternalHeaderFollow)
                return await AdvanceByWalkerAsync(
                    tip, cursor, bundle, executor, options, pollInterval,
                    lastCommittedBlock, lastCommittedHash, lastSeenTipBlock, lastSeenTipHash,
                    blocksExecuted, rootMismatches, ct, logger).ConfigureAwait(false);

            var externalReorg = await UnderGateAsync(options, ct, async () =>
            {
                var gate = options.ChainWriteGate;
                var committedBlock = gate != null && gate.HasHead ? gate.HeadNumber : lastCommittedBlock;
                var committedHash = gate != null && gate.HasHead ? gate.HeadHash ?? lastCommittedHash : lastCommittedHash;
                var rewound = await ReorgRewindCoordinator.TryRewindOnExternalReorgAsync(
                bundle, executor, options, committedBlock, committedHash,
                blocksExecuted, rootMismatches, ct, logger).ConfigureAwait(false);
                if (rewound is ReorgRewindCoordinator.ForwardRewindOutcome applied && applied.Terminal == null)
                    options.ChainWriteGate?.SetHead(applied.NewHead, applied.NewHeadHash ?? lastCommittedHash);
                return rewound;
            }).ConfigureAwait(false);
            if (externalReorg is ReorgRewindCoordinator.ForwardRewindOutcome reorgOutcome)
            {
                if (reorgOutcome.Terminal == null)
                    await AfterAncestorRewindAsync(bundle, reorgOutcome.NewHead, tip.BlockNumber, ct, logger)
                        .ConfigureAwait(false);
                return AfterRepair(
                    reorgOutcome.Terminal, reorgOutcome.NewHead, reorgOutcome.NewHeadHash ?? lastCommittedHash,
                    lastSeenTipBlock, lastSeenTipHash);
            }

            return HeaderSubchainAdvance.Ready(lastCommittedBlock, lastCommittedHash, lastSeenTipBlock, lastSeenTipHash);
        }

        private async Task<HeaderSubchainAdvance> AdvanceByWalkerAsync(
            CanonicalTip tip,
            ulong cursor,
            IChainStoreBundle bundle,
            IBlockExecutor executor,
            FollowerOptions options,
            TimeSpan pollInterval,
            ulong lastCommittedBlock,
            byte[] lastCommittedHash,
            ulong lastSeenTipBlock,
            byte[] lastSeenTipHash,
            ulong blocksExecuted,
            ulong rootMismatches,
            CancellationToken ct,
            ILogger logger)
        {
            WalkerOutcome walkResult;
            try
            {
                walkResult = await _walker(
                    fromBlockNumber: tip.BlockNumber,
                    fromHash: tip.BlockHash,
                    toBlockNumber: cursor,
                    bundle: bundle,
                    ct: ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "snap.walker.exception tip={Tip}; backing off and retrying",
                    tip.BlockNumber);
                await Task.Delay(pollInterval, ct).ConfigureAwait(false);
                return HeaderSubchainAdvance.Retry(lastCommittedBlock, lastCommittedHash, lastSeenTipBlock, lastSeenTipHash);
            }

            return await AdvanceAfterWalkAsync(
                walkResult, tip, bundle, executor, options, pollInterval,
                lastCommittedBlock, lastCommittedHash, lastSeenTipBlock, lastSeenTipHash,
                blocksExecuted, rootMismatches, ct, logger).ConfigureAwait(false);
        }

        private async Task<HeaderSubchainAdvance> AdvanceAfterWalkAsync(
            WalkerOutcome walkResult,
            CanonicalTip tip,
            IChainStoreBundle bundle,
            IBlockExecutor executor,
            FollowerOptions options,
            TimeSpan pollInterval,
            ulong lastCommittedBlock,
            byte[] lastCommittedHash,
            ulong lastSeenTipBlock,
            byte[] lastSeenTipHash,
            ulong blocksExecuted,
            ulong rootMismatches,
            CancellationToken ct,
            ILogger logger)
        {
            if (walkResult != null)
            {
                logger.LogInformation(
                    "snap.walker.exit reason={ExitReason} blocks_walked={BlocksWalked}",
                    walkResult.ExitReason, walkResult.HeadersWritten);

                switch (ClassifyWalkerOutcome(walkResult, ct))
                {
                    case WalkerDisposition.Backoff:
                        ForceTipRepollNextCycle(ref lastSeenTipBlock, ref lastSeenTipHash);
                        await Task.Delay(pollInterval, ct).ConfigureAwait(false);
                        return HeaderSubchainAdvance.Retry(
                            lastCommittedBlock, lastCommittedHash, lastSeenTipBlock, lastSeenTipHash);

                    case WalkerDisposition.RepairDivergence:
                        var repair = await RepairDivergenceAsync(
                            walkResult, tip, bundle, executor, options,
                            lastCommittedBlock, lastCommittedHash, blocksExecuted, rootMismatches,
                            ct, logger).ConfigureAwait(false);
                        return AfterRepair(
                            repair.Terminal, repair.NewHead, repair.NewHeadHash, lastSeenTipBlock, lastSeenTipHash);
                }

                RecordSkeletonSegment(bundle, tip);
            }

            return HeaderSubchainAdvance.Ready(lastCommittedBlock, lastCommittedHash, lastSeenTipBlock, lastSeenTipHash);
        }

        private readonly struct ConfirmedRangeExecution
        {
            public TipCycleSignal Signal { get; init; }
            public FollowerRunResult Terminal { get; init; }
            public ulong LastCommittedBlock { get; init; }
            public byte[] LastCommittedHash { get; init; }
            public ulong LastSeenTipBlock { get; init; }
            public byte[] LastSeenTipHash { get; init; }
            public ulong BlocksExecuted { get; init; }
            public ulong RootMismatches { get; init; }
            public ulong ConsecutiveDivergences { get; init; }
            public ulong RewindCyclesUsed { get; init; }
            public int ConsecutiveBodyRepairs { get; init; }
            public IBlockExecutor Executor { get; init; }
            public ulong? BodyHoleAt { get; init; }
        }

        private async Task<ConfirmedRangeExecution> ExecuteConfirmedRangeAsync(
            CanonicalTip tip,
            ulong cursor,
            IChainStoreBundle bundle,
            IBlockExecutor executor,
            Func<IChainStoreBundle, IBlockExecutor> executorFactory,
            FollowerOptions options,
            ulong lastCommittedBlock,
            byte[] lastCommittedHash,
            ulong lastSeenTipBlock,
            byte[] lastSeenTipHash,
            ulong blocksExecuted,
            ulong rootMismatches,
            ulong consecutiveDivergences,
            ulong rewindCyclesUsed,
            int consecutiveBodyRepairs,
            CancellationToken ct,
            ILogger logger)
        {
            ulong from = cursor + 1;
            ulong to = tip.BlockNumber;
            if (to >= from)
            {
                var blocksExecutedBefore = blocksExecuted;
                var forwardSource = new OrderingBlockSource(bundle, from, to, lastCommittedHash);
                var forwardResult = await ExecuteForwardAsync(
                    forwardSource, bundle, executor, options,
                    lastCommittedBlock, lastCommittedHash, blocksExecuted, rootMismatches,
                    consecutiveDivergences, rewindCyclesUsed,
                    ct, logger).ConfigureAwait(false);
                lastCommittedBlock = forwardResult.LastCommitted;
                lastCommittedHash = forwardResult.LastCommittedHash ?? lastCommittedHash;
                blocksExecuted = forwardResult.BlocksExecuted;
                rootMismatches = forwardResult.RootMismatches;
                consecutiveDivergences = forwardResult.ConsecutiveDivergences;

                if (forwardResult.Terminal is FollowerRunResult terminal)
                {
                    return BuildRangeExecution(
                        TipCycleSignal.Proceed, terminal,
                        lastCommittedBlock, lastCommittedHash, lastSeenTipBlock, lastSeenTipHash,
                        blocksExecuted, rootMismatches, consecutiveDivergences, rewindCyclesUsed,
                        consecutiveBodyRepairs, executor);
                }

                if (forwardResult.BodyHoleAt is ulong holeBlock)
                {
                    if (MadeForwardProgress(blocksExecuted, blocksExecutedBefore))
                        consecutiveBodyRepairs = 0;
                    return BuildRangeExecution(
                        TipCycleSignal.Proceed, null,
                        lastCommittedBlock, lastCommittedHash, lastSeenTipBlock, lastSeenTipHash,
                        blocksExecuted, rootMismatches, consecutiveDivergences, rewindCyclesUsed,
                        consecutiveBodyRepairs, executor, holeBlock);
                }

                if (forwardResult.Rewound)
                {
                    var rewindHalt = CountRewindCycleOrHalt(
                        ref rewindCyclesUsed, options, lastCommittedBlock, blocksExecuted, rootMismatches);
                    if (rewindHalt != null)
                        return BuildRangeExecution(
                            TipCycleSignal.Proceed, rewindHalt,
                            lastCommittedBlock, lastCommittedHash, lastSeenTipBlock, lastSeenTipHash,
                            blocksExecuted, rootMismatches, consecutiveDivergences, rewindCyclesUsed,
                            consecutiveBodyRepairs, executor);
                    executor = executorFactory(bundle);
                    options.ChainWriteGate?.Publish(executor);
                    ForceTipRepollNextCycle(ref lastSeenTipBlock, ref lastSeenTipHash);
                    return BuildRangeExecution(
                        TipCycleSignal.Continue, null,
                        lastCommittedBlock, lastCommittedHash, lastSeenTipBlock, lastSeenTipHash,
                        blocksExecuted, rootMismatches, consecutiveDivergences, rewindCyclesUsed,
                        consecutiveBodyRepairs, executor);
                }

                if (MadeForwardProgress(blocksExecuted, blocksExecutedBefore))
                {
                    rewindCyclesUsed = 0;
                    consecutiveBodyRepairs = 0;
                }
            }

            return BuildRangeExecution(
                TipCycleSignal.Proceed, null,
                lastCommittedBlock, lastCommittedHash, lastSeenTipBlock, lastSeenTipHash,
                blocksExecuted, rootMismatches, consecutiveDivergences, rewindCyclesUsed,
                consecutiveBodyRepairs, executor);
        }

        private static ConfirmedRangeExecution BuildRangeExecution(
            TipCycleSignal signal,
            FollowerRunResult terminal,
            ulong lastCommittedBlock,
            byte[] lastCommittedHash,
            ulong lastSeenTipBlock,
            byte[] lastSeenTipHash,
            ulong blocksExecuted,
            ulong rootMismatches,
            ulong consecutiveDivergences,
            ulong rewindCyclesUsed,
            int consecutiveBodyRepairs,
            IBlockExecutor executor,
            ulong? bodyHoleAt = null)
            => new ConfirmedRangeExecution
            {
                Signal = signal,
                Terminal = terminal,
                LastCommittedBlock = lastCommittedBlock,
                LastCommittedHash = lastCommittedHash,
                LastSeenTipBlock = lastSeenTipBlock,
                LastSeenTipHash = lastSeenTipHash,
                BlocksExecuted = blocksExecuted,
                RootMismatches = rootMismatches,
                ConsecutiveDivergences = consecutiveDivergences,
                RewindCyclesUsed = rewindCyclesUsed,
                ConsecutiveBodyRepairs = consecutiveBodyRepairs,
                Executor = executor,
                BodyHoleAt = bodyHoleAt,
            };

        private enum WalkerDisposition { Proceed, RepairDivergence, Backoff }

        private static WalkerDisposition ClassifyWalkerOutcome(WalkerOutcome walkResult, CancellationToken ct)
        {
            switch (walkResult.ExitReason)
            {
                case WalkerExitReason.LastKnownGoodDivergence:
                    return WalkerDisposition.RepairDivergence;

                case WalkerExitReason.PeerPoolEmpty:
                    return WalkerDisposition.Backoff;

                case WalkerExitReason.MetExistingStore:
                case WalkerExitReason.ReachedTarget:
                case WalkerExitReason.StructuralGenesis:
                    return WalkerDisposition.Proceed;

                case WalkerExitReason.Cancelled:
                    throw new OperationCanceledException(ct);
            }

            return WalkerDisposition.Proceed;
        }

        private readonly struct DivergenceRepairOutcome
        {
            public DivergenceRepairOutcome(FollowerRunResult terminal, ulong newHead, byte[] newHeadHash)
            {
                Terminal = terminal;
                NewHead = newHead;
                NewHeadHash = newHeadHash;
            }

            public FollowerRunResult Terminal { get; }
            public ulong NewHead { get; }
            public byte[] NewHeadHash { get; }
        }

        private async Task<DivergenceRepairOutcome> RepairDivergenceAsync(
            WalkerOutcome walkResult,
            CanonicalTip tip,
            IChainStoreBundle bundle,
            IBlockExecutor executor,
            FollowerOptions options,
            ulong lastCommittedBlock,
            byte[] lastCommittedHash,
            ulong blocksExecuted,
            ulong rootMismatches,
            CancellationToken ct,
            ILogger logger)
        {
            var refusal = RefuseDivergenceWithNoResolver(
                walkResult, lastCommittedBlock, blocksExecuted, rootMismatches, logger);
            if (refusal != null)
                return new DivergenceRepairOutcome(refusal, lastCommittedBlock, lastCommittedHash);

            logger.LogWarning(
                "snap.walker.divergence at block {Block}; invoking findAncestor",
                walkResult.DivergenceBlock);

            ulong divergedBlock = walkResult.DivergenceBlock.Value;
            bool relaid = false;
            for (int repairAttempt = 0; repairAttempt < MaxDivergenceRepairAttempts && !relaid; repairAttempt++)
            {
                ulong floorBlock = AnchorAncestorSearchFloor(lastCommittedBlock, divergedBlock, options);

                var resolution = await ResolveAncestorAsync(
                    divergedBlock, floorBlock, lastCommittedBlock, blocksExecuted, rootMismatches, ct, logger)
                    .ConfigureAwait(false);
                if (resolution.Terminal is FollowerRunResult resolverTerminal)
                    return new DivergenceRepairOutcome(resolverTerminal, lastCommittedBlock, lastCommittedHash);

                ulong ancestorBlock = resolution.AncestorBlock;

                var ancestorHash = await bundle.Blocks
                    .GetHashByNumberAsync(new BigInteger(ancestorBlock))
                    .ConfigureAwait(false);

                var rewind = await UnderGateAsync(options, ct, async () =>
                {
                    var rewound = await ReorgRewindCoordinator.RewindToAncestorAsync(
                    bundle, executor, ancestorBlock, RewindScope.ExecutionAndChain,
                    lastCommittedBlock, blocksExecuted, rootMismatches, logger, ct).ConfigureAwait(false);
                    if (rewound.Terminal == null)
                        options.ChainWriteGate?.SetHead(ancestorBlock, ancestorHash ?? lastCommittedHash);
                    return rewound;
                }).ConfigureAwait(false);
                if (rewind.Terminal != null)
                    return new DivergenceRepairOutcome(rewind.Terminal, lastCommittedBlock, lastCommittedHash);

                lastCommittedBlock = ancestorBlock;
                lastCommittedHash = ancestorHash ?? lastCommittedHash;

                logger.LogInformation(
                    "snap.ancestor.rewound block={Ancestor} diverged={Diverged} attempt={Attempt}",
                    ancestorBlock, divergedBlock, repairAttempt + 1);

                var relay = await ForceRelayAboveAncestorAsync(tip, ancestorBlock, bundle, ct, logger)
                    .ConfigureAwait(false);
                if (relay.DeeperDivergence is ulong deeperDivergence)
                {
                    divergedBlock = deeperDivergence;
                    continue;
                }
                relaid = relay.Relaid;
            }

            if (relaid)
                await AfterAncestorRewindAsync(bundle, lastCommittedBlock, tip.BlockNumber, ct, logger)
                    .ConfigureAwait(false);

            var exhausted = HaltOnExhaustedRepairBudget(
                relaid, divergedBlock, lastCommittedBlock, blocksExecuted, rootMismatches, logger);
            return new DivergenceRepairOutcome(exhausted, lastCommittedBlock, lastCommittedHash);
        }

        private FollowerRunResult RefuseDivergenceWithNoResolver(
            WalkerOutcome walkResult, ulong lastCommittedBlock, ulong blocksExecuted, ulong rootMismatches,
            ILogger logger)
        {
            if (_ancestorResolver != null && walkResult.DivergenceBlock != null) return null;

            logger.LogError(
                "snap.walker.divergence at block {Block}; no ancestor resolver wired — halting",
                walkResult.DivergenceBlock);
            return new FollowerRunResult(
                FollowerExitReason.FatalVerdict,
                lastCommittedBlock, blocksExecuted, rootMismatches, RewindCyclesUsed: 0,
                SnapshotRestoreTarget: null,
                Detail: $"backward-walker reported divergence at block {walkResult.DivergenceBlock}; " +
                        "findAncestor binary search not yet implemented — manual rewind required");
        }

        private readonly struct AncestorResolution
        {
            public AncestorResolution(ulong ancestorBlock, FollowerRunResult terminal)
            {
                AncestorBlock = ancestorBlock;
                Terminal = terminal;
            }

            public ulong AncestorBlock { get; }
            public FollowerRunResult Terminal { get; }
        }

        private async Task<AncestorResolution> ResolveAncestorAsync(
            ulong divergedBlock, ulong floorBlock, ulong lastCommittedBlock,
            ulong blocksExecuted, ulong rootMismatches, CancellationToken ct, ILogger logger)
        {
            try
            {
                return new AncestorResolution(
                    await _ancestorResolver(divergedBlock, floorBlock, ct).ConfigureAwait(false),
                    terminal: null);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception resolverEx)
            {
                logger.LogError(resolverEx,
                    "snap.ancestor.resolver_failed diverged={Diverged} floor={Floor}; halting",
                    divergedBlock, floorBlock);
                return new AncestorResolution(0UL, new FollowerRunResult(
                    FollowerExitReason.FatalVerdict,
                    lastCommittedBlock, blocksExecuted, rootMismatches, RewindCyclesUsed: 0,
                    SnapshotRestoreTarget: null,
                    Detail: $"ancestor resolver threw {resolverEx.GetType().Name}: {resolverEx.Message}"));
            }
        }

        private readonly struct RelayOutcome
        {
            public RelayOutcome(bool relaid, ulong? deeperDivergence)
            {
                Relaid = relaid;
                DeeperDivergence = deeperDivergence;
            }

            public bool Relaid { get; }
            public ulong? DeeperDivergence { get; }
        }

        private async Task<RelayOutcome> ForceRelayAboveAncestorAsync(
            CanonicalTip tip, ulong ancestorBlock, IChainStoreBundle bundle, CancellationToken ct, ILogger logger)
        {
            try
            {
                var relay = await _walker(
                    tip.BlockNumber, tip.BlockHash, ancestorBlock, bundle, ct,
                    noShortCircuitAboveBlock: ancestorBlock).ConfigureAwait(false);
                if (relay.ExitReason == WalkerExitReason.LastKnownGoodDivergence
                    && relay.DivergenceBlock != null)
                {
                    logger.LogWarning(
                        "snap.ancestor.relay_diverged at={Block} ancestor={Ancestor} — resolved ancestor not canonical; re-resolving deeper",
                        relay.DivergenceBlock, ancestorBlock);
                    return new RelayOutcome(relaid: false, deeperDivergence: relay.DivergenceBlock.Value);
                }
                logger.LogInformation(
                    "snap.ancestor.relaid reason={ExitReason} headers={Headers} range=({Ancestor}..{Tip}]",
                    relay.ExitReason, relay.HeadersWritten, ancestorBlock, tip.BlockNumber);
                return new RelayOutcome(relaid: true, deeperDivergence: null);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception relayEx)
            {
                logger.LogWarning(relayEx,
                    "snap.ancestor.relay_failed range=({Ancestor}..{Tip}]; retrying next poll",
                    ancestorBlock, tip.BlockNumber);
                return new RelayOutcome(relaid: true, deeperDivergence: null);
            }
        }

        private static FollowerRunResult HaltOnExhaustedRepairBudget(
            bool relaid, ulong divergedBlock, ulong lastCommittedBlock,
            ulong blocksExecuted, ulong rootMismatches, ILogger logger)
        {
            if (relaid) return null;

            logger.LogError(
                "snap.ancestor.repair_exhausted diverged={Diverged} after={Attempts} attempts; halting",
                divergedBlock, MaxDivergenceRepairAttempts);
            return new FollowerRunResult(
                FollowerExitReason.FatalVerdict,
                lastCommittedBlock, blocksExecuted, rootMismatches, RewindCyclesUsed: 0,
                SnapshotRestoreTarget: null,
                Detail: $"reorg repair did not link after {MaxDivergenceRepairAttempts} ancestor-resolve/re-lay attempts " +
                        $"(last divergence at {divergedBlock}) — fork deeper than the search window; manual rewind required");
        }

        private readonly struct BodyRepairOutcome
        {
            public BodyRepairOutcome(FollowerRunResult terminal, int newRepairCount)
            {
                Terminal = terminal;
                NewRepairCount = newRepairCount;
            }

            public FollowerRunResult Terminal { get; }
            public int NewRepairCount { get; }
        }

        private async Task<BodyRepairOutcome> RepairBodyHoleAsync(
            ulong holeBlock,
            ulong to,
            IChainStoreBundle bundle,
            FollowerOptions options,
            int consecutiveBodyRepairs,
            ulong lastCommittedBlock,
            ulong blocksExecuted,
            ulong rootMismatches,
            ulong rewindCyclesUsed,
            CancellationToken ct,
            ILogger logger)
        {
            if (_bodyRepair == null)
            {
                return new BodyRepairOutcome(new FollowerRunResult(
                    FollowerExitReason.FatalVerdict,
                    lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                    SnapshotRestoreTarget: null,
                    Detail: $"body integrity fault at block {holeBlock:N0} and no body-repair delegate wired; " +
                            "refusing to execute a body that does not match its header"),
                    consecutiveBodyRepairs);
            }
            consecutiveBodyRepairs++;
            if (ExceededSourceFailureBudget(consecutiveBodyRepairs, options))
            {
                return new BodyRepairOutcome(new FollowerRunResult(
                    FollowerExitReason.SourceUnavailable,
                    lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                    SnapshotRestoreTarget: null,
                    Detail: $"body repair not converging at block {holeBlock:N0} after {consecutiveBodyRepairs} attempts"),
                    consecutiveBodyRepairs);
            }
            bool repaired = false;
            try
            {
                repaired = await _bodyRepair(holeBlock, to, bundle, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception repairEx)
            {
                logger.LogWarning(repairEx,
                    "snap.tip.body_repair_failed from={From} to={To}; retrying next tick",
                    holeBlock, to);
            }
            logger.LogInformation(
                "snap.tip.body_repair from={From} to={To} repaired={Repaired} attempt={Attempt}",
                holeBlock, to, repaired, consecutiveBodyRepairs);
            return new BodyRepairOutcome(terminal: null, consecutiveBodyRepairs);
        }

        private static FollowerRunResult CountRewindCycleOrHalt(
            ref ulong rewindCyclesUsed, FollowerOptions options,
            ulong lastCommittedBlock, ulong blocksExecuted, ulong rootMismatches)
        {
            rewindCyclesUsed++;
            if (rewindCyclesUsed <= (ulong)options.MaxRewindCycles) return null;

            return new FollowerRunResult(
                FollowerExitReason.FatalVerdict,
                lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                SnapshotRestoreTarget: null,
                Detail: $"exceeded MaxRewindCycles ({options.MaxRewindCycles}) without forward progress; " +
                        $"repeated state-root divergence above block {lastCommittedBlock:N0}");
        }

        private static async Task<(CanonicalTip Tip, Exception Failure)> PollCanonicalTipAsync(
            ICanonicalStateRootSource canonical, TimeSpan pollInterval, CancellationToken ct, ILogger logger)
        {
            try
            {
                return (await canonical.GetLatestAsync(ct).ConfigureAwait(false), null);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogDebug(ex,
                    "snap.canonical.poll_failed source={Source}; retrying in {Interval}",
                    canonical.Name, pollInterval);
                return (null, ex);
            }
        }

        private static bool ExceededSourceFailureBudget(int consecutiveFailures, FollowerOptions options)
            => consecutiveFailures > options.MaxConsecutiveSourceFailures;

        private static string DescribeSourceFailure(Exception lastSourceException)
            => lastSourceException != null
                ? $"{lastSourceException.GetType().Name}: {lastSourceException.Message}"
                : "GetLatestAsync returned null";

        private static bool TipOffersNothingNew(
            CanonicalTip tip, ulong lastCommittedBlock, ulong lastSeenTipBlock, byte[] lastSeenTipHash)
            => tip.BlockNumber <= lastCommittedBlock
                || (lastSeenTipHash != null && tip.BlockHash != null && ByteUtil.AreEqual(tip.BlockHash, lastSeenTipHash) && tip.BlockNumber == lastSeenTipBlock);

        private static bool BelowWalkerInvocationThreshold(ulong delta, FollowerOptions options)
            => delta < options.WalkerInvocationThreshold;

        private static bool MadeForwardProgress(ulong blocksExecuted, ulong blocksExecutedBefore)
            => blocksExecuted > blocksExecutedBefore;

        private static bool ReachedConfiguredEndBlock(ulong lastCommittedBlock, FollowerOptions options)
            => options.EndBlock.HasValue && lastCommittedBlock >= options.EndBlock.Value;

        private static void ForceTipRepollNextCycle(ref ulong lastSeenTipBlock, ref byte[] lastSeenTipHash)
        {
            lastSeenTipBlock = 0UL;
            lastSeenTipHash = null;
        }

        private static FollowerRunResult SpendSourceFailureBudget(
            ref int consecutiveSourceFailures, FollowerOptions options,
            ICanonicalStateRootSource canonical, Exception lastSourceException,
            ulong lastCommittedBlock, ulong blocksExecuted, ulong rootMismatches, ILogger logger)
        {
            consecutiveSourceFailures++;
            if (ExceededSourceFailureBudget(consecutiveSourceFailures, options))
            {
                var reason = DescribeSourceFailure(lastSourceException);
                logger.LogError(
                    "snap.canonical.unreachable source={Source} after={Failures} retries; aborting follower ({Reason})",
                    canonical.Name, consecutiveSourceFailures, reason);
                return new FollowerRunResult(
                    FollowerExitReason.SourceUnavailable,
                    lastCommittedBlock, blocksExecuted, rootMismatches, RewindCyclesUsed: 0,
                    SnapshotRestoreTarget: null,
                    Detail: $"canonical source {canonical.Name} unreachable after {consecutiveSourceFailures} consecutive polls ({reason})");
            }
            return null;
        }

        private static ulong AnchorAncestorSearchFloor(
            ulong lastCommittedBlock, ulong divergedBlock, FollowerOptions options)
        {
            ulong floorBlock = lastCommittedBlock > options.MaxReorgDepth
                ? lastCommittedBlock - options.MaxReorgDepth
                : 0UL;
            if (floorBlock > divergedBlock) floorBlock = divergedBlock;
            return floorBlock;
        }

        private async Task AfterAncestorRewindAsync(
            IChainStoreBundle bundle, ulong newHead, ulong tipBlock, CancellationToken ct, ILogger logger)
        {
            if (_bodyRepair == null) return;
            try
            {
                await _bodyRepair(newHead + 1, tipBlock, bundle, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "snap.tip.requeue_failed from={From} to={To}; the tip band refills on its next pass",
                    newHead + 1, tipBlock);
            }
        }

        private static void SeedHeaderSubchainFromFrontier(
            IChainStoreBundle bundle, FollowerOptions options, ulong lastCommittedBlock)
        {
            if (!options.ExternalHeaderFollow
                && lastCommittedBlock > 0 && bundle.Metadata.GetHeaderSyncState().Subchains.Count == 0)
            {
                bundle.Metadata.SaveHeaderSyncState(new HeaderSyncState
                {
                    SchemaVersion = HeaderSyncStateRlpEncoder.CurrentSchemaVersion,
                    Subchains = new[] { new HeaderSubchain { Head = lastCommittedBlock, Tail = 0, Next = 0 } },
                });
            }
        }

        private static void RecordSkeletonSegment(IChainStoreBundle bundle, CanonicalTip tip)
        {
            var hss = bundle.Metadata.GetHeaderSyncState();
            var prevTop = HeaderSubchains.TrustedTip(hss);
            hss = HeaderSubchains.OpenTip(hss, tip.BlockNumber);
            hss = HeaderSubchains.RecordDescent(hss, tip.BlockNumber, prevTop > 0 ? prevTop + 1 : 0);
            bundle.Metadata.SaveHeaderSyncState(hss);
        }

        private readonly struct ForwardExecOutcome
        {
            public ForwardExecOutcome(ulong lastCommitted, byte[] lastCommittedHash,
                ulong blocksExecuted, ulong rootMismatches, FollowerRunResult terminal, bool rewound = false,
                ulong consecutiveDivergences = 0, ulong? bodyHoleAt = null)
            {
                LastCommitted = lastCommitted;
                LastCommittedHash = lastCommittedHash;
                BlocksExecuted = blocksExecuted;
                RootMismatches = rootMismatches;
                Terminal = terminal;
                Rewound = rewound;
                ConsecutiveDivergences = consecutiveDivergences;
                BodyHoleAt = bodyHoleAt;
            }
            public ulong LastCommitted { get; }
            public byte[] LastCommittedHash { get; }
            public ulong BlocksExecuted { get; }
            public ulong RootMismatches { get; }
            public FollowerRunResult Terminal { get; }
            public bool Rewound { get; }
            public ulong ConsecutiveDivergences { get; }
            public ulong? BodyHoleAt { get; }
        }

        private async Task<ReorgRewindCoordinator.ForwardRewindOutcome> RewindOnRootMismatchAsync(
            IChainStoreBundle bundle,
            FollowerOptions options,
            ulong lastCommittedBlock,
            byte[] lastCommittedHash,
            ulong divergedBlock,
            ulong blocksExecuted,
            ulong rootMismatches,
            CancellationToken ct,
            ILogger logger)
        {
            if (lastCommittedBlock == 0)
                return new ReorgRewindCoordinator.ForwardRewindOutcome(
                    lastCommittedBlock, null,
                    new FollowerRunResult(
                        FollowerExitReason.FatalVerdict,
                        lastCommittedBlock, blocksExecuted, rootMismatches, RewindCyclesUsed: 0,
                        SnapshotRestoreTarget: null,
                        Detail: $"tip-driven forward exec: state-root mismatch at block {divergedBlock:N0}; no rewind target above genesis"));

            logger.LogWarning("snap.tip.root_mismatch block={Block}; rewinding", divergedBlock);

            ReorgRewindCoordinator.RewindLoopOutcome outcome;
            try
            {
                outcome = await ReorgRewindCoordinator.ValidatingRewindAsync(
                    bundle, null, options, lastCommittedBlock, lastCommittedHash,
                    blocksExecuted, rootMismatches, 0UL, ct, _ancestorResolver, divergedBlock, logger,
                    options.ExternalHeaderFollow ? RewindScope.ExecutionOnly : RewindScope.ExecutionAndChain)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "snap.tip.rewind_failed block={Block}", divergedBlock);
                return new ReorgRewindCoordinator.ForwardRewindOutcome(
                    bundle.Metadata.GetLastBlock(), bundle.Metadata.GetLastBlockHash(),
                    new FollowerRunResult(
                        FollowerExitReason.FatalVerdict,
                        lastCommittedBlock, blocksExecuted, rootMismatches, RewindCyclesUsed: 0,
                        SnapshotRestoreTarget: null,
                        Detail: $"tip-driven forward exec: rewind threw {ex.GetType().Name}: {ex.Message}"));
            }

            if (outcome.TerminalResult == null)
                logger.LogInformation("snap.tip.rewound new_head={Head}", outcome.NewHead);
            return new ReorgRewindCoordinator.ForwardRewindOutcome(
                outcome.NewHead, outcome.NewHeadHash, outcome.TerminalResult);
        }

        private static string DescribeBodyIntegrityFault(BlockHeader header, BlockBundle blockBundle)
        {
            if (header.TransactionsHash != null && header.TransactionsHash.Length == 32)
            {
                var computed = _bodyRootsProvider.CalculateTransactionsRoot(
                    blockBundle.Transactions ?? new List<ISignedTransaction>());
                if (!ByteUtil.AreEqual(computed, header.TransactionsHash))
                    return $"transactions root mismatch (header=0x{header.TransactionsHash.ToHex()} computed=0x{computed.ToHex()} txs={blockBundle.Transactions?.Count ?? 0})";
            }

            if (header.WithdrawalsRoot != null && header.WithdrawalsRoot.Length == 32)
            {
                var computed = _bodyRootsProvider.CalculateWithdrawalsRoot(
                    blockBundle.Withdrawals ?? new List<Withdrawal>());
                if (!ByteUtil.AreEqual(computed, header.WithdrawalsRoot))
                    return $"withdrawals root mismatch (header=0x{header.WithdrawalsRoot.ToHex()} computed=0x{computed.ToHex()} withdrawals={blockBundle.Withdrawals?.Count ?? 0})";
            }

            return null;
        }

        private async Task<ForwardExecOutcome> ExecuteForwardAsync(
            IBlockSource source,
            IChainStoreBundle bundle,
            IBlockExecutor executor,
            FollowerOptions options,
            ulong lastCommittedBlock,
            byte[] lastCommittedHash,
            ulong blocksExecuted,
            ulong rootMismatches,
            ulong consecutiveDivergences,
            ulong rewindCyclesUsed,
            CancellationToken ct,
            ILogger logger)
        {
            await foreach (var blockBundle in source.StreamAsync(lastCommittedBlock + 1, ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                var header = blockBundle.Header;
                var blockNumber = (ulong)header.BlockNumber;

                var gate = options.ChainWriteGate;
                if (gate != null) await gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    if (gate != null)
                    {
                        AdoptGateHead(gate, ref lastCommittedBlock, ref lastCommittedHash, ref blocksExecuted);

                        if (lastCommittedBlock >= blockNumber) continue;

                        if (blockNumber != lastCommittedBlock + 1)
                        {
                            return new ForwardExecOutcome(
                                lastCommittedBlock, lastCommittedHash, blocksExecuted, rootMismatches, null,
                                consecutiveDivergences: consecutiveDivergences);
                        }
                    }

                    var bodyFault = DescribeBodyIntegrityFault(header, blockBundle);
                    if (bodyFault != null)
                    {
                        logger.LogWarning(
                            "snap.tip.body_integrity block={Block} {Fault} — deferring execution for body repair",
                            blockNumber, bodyFault);
                        return new ForwardExecOutcome(
                            lastCommittedBlock, lastCommittedHash, blocksExecuted, rootMismatches,
                            terminal: null,
                            consecutiveDivergences: consecutiveDivergences,
                            bodyHoleAt: blockNumber);
                    }

                    (bundle as IAtomicBlockFlush)?.ArmWithdrawals(blockNumber, blockBundle.Withdrawals);
                    var result = await executor.ProcessBlockAsync(
                        header,
                        blockBundle.Transactions,
                        blockBundle.Uncles,
                        blockBundle.Withdrawals,
                        ct).ConfigureAwait(false);

                    if (result.RootMatches)
                    {
                        lastCommittedBlock = blockNumber;
                        lastCommittedHash = blockBundle.HeaderHash;
                        bundle.Metadata.SaveExecutedHash(blockNumber, blockBundle.HeaderHash);
                        gate?.SetHead(blockNumber, blockBundle.HeaderHash);
                        blocksExecuted++;
                        consecutiveDivergences = 0;
                        await CommitMatchedBlockAsync(
                            bundle, blockNumber, blockBundle.HeaderHash, blockBundle,
                            result.ComputedStateRoot, options, logger, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        logger.LogError(
                            "Block {Block} rejected: {Reason}. Rewinding.",
                            blockNumber, result.DescribeRejection());

                        rootMismatches++;
                        consecutiveDivergences++;
                        if (consecutiveDivergences > (ulong)options.MaxConsecutiveDivergences)
                        {
                            return new ForwardExecOutcome(
                                lastCommittedBlock, lastCommittedHash, blocksExecuted, rootMismatches,
                                new FollowerRunResult(
                                    FollowerExitReason.FatalVerdict,
                                    lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                                    SnapshotRestoreTarget: null,
                                    Detail: $"max consecutive divergences ({options.MaxConsecutiveDivergences}) exceeded at block {blockNumber:N0}"),
                                consecutiveDivergences: consecutiveDivergences);
                        }

                        var rewindOutcome = await RewindOnRootMismatchAsync(
                            bundle, options, lastCommittedBlock, lastCommittedHash, blockNumber,
                            blocksExecuted, rootMismatches, ct, logger).ConfigureAwait(false);

                        if (rewindOutcome.Terminal == null)
                            gate?.SetHead(rewindOutcome.NewHead, rewindOutcome.NewHeadHash ?? lastCommittedHash);

                        if (rewindOutcome.Terminal is FollowerRunResult terminalResult)
                        {
                            return new ForwardExecOutcome(
                                rewindOutcome.NewHead,
                                rewindOutcome.NewHeadHash ?? lastCommittedHash,
                                blocksExecuted, rootMismatches,
                                terminalResult,
                                consecutiveDivergences: consecutiveDivergences);
                        }

                        return new ForwardExecOutcome(
                            rewindOutcome.NewHead,
                            rewindOutcome.NewHeadHash ?? lastCommittedHash,
                            blocksExecuted, rootMismatches,
                            terminal: null,
                            rewound: true,
                            consecutiveDivergences: consecutiveDivergences);
                    }
                }
                finally
                {
                    gate?.Release();
                }
            }
            return new ForwardExecOutcome(
                lastCommittedBlock, lastCommittedHash, blocksExecuted, rootMismatches, null,
                consecutiveDivergences: consecutiveDivergences);
        }
    }
}
