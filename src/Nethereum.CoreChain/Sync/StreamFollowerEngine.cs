using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.CoreChain.Sync
{
    internal sealed class StreamFollowerEngine : FollowerEngineBase
    {
        private static readonly TimeSpan SourceRetryBackoff = TimeSpan.FromSeconds(5);

        private static bool ExceededSourceFailureBudget(int consecutiveFailures, FollowerOptions options)
            => consecutiveFailures > options.MaxConsecutiveSourceFailures;

        private static bool ExceededDivergenceBudget(int consecutiveDivergences, FollowerOptions options)
            => consecutiveDivergences > options.MaxConsecutiveDivergences;

        private static string DescribeException(Exception ex) => $"{ex.GetType().Name}: {ex.Message}";

        private static FollowerRunResult RunTerminal(
            FollowerExitReason reason,
            ulong lastCommittedBlock,
            ulong blocksExecuted,
            ulong rootMismatches,
            ulong rewindCyclesUsed,
            string detail)
            => new FollowerRunResult(
                reason,
                lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                SnapshotRestoreTarget: null,
                Detail: detail);

        internal async Task<FollowerRunResult> RunAsync(
            IBlockSource source,
            IChainStoreBundle bundle,
            IBlockExecutor executor,
            Func<IChainStoreBundle, IBlockExecutor> executorFactory,
            IValidationPolicy policy,
            ICanonicalStateRootSource canonical,
            FollowerOptions options,
            ulong lastCommittedBlock,
            byte[] lastCommittedHash,
            ulong currentStart,
            CancellationToken ct,
            ILogger logger,
            IChainForkChoice forkChoice = null,
            IMempoolReorgReconciler mempoolReconciler = null)
        {
            ulong blocksExecuted = 0UL;
            ulong rootMismatches = 0UL;
            ulong rewindCyclesUsed = 0UL;
            int consecutiveDivergences = 0;

            try
            {
                int consecutiveSourceFailures = 0;
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    bool restartLoop = false;

                    IAsyncEnumerable<BlockBundle> stream;
                    try
                    {
                        stream = source.StreamAsync(currentStart, ct);
                    }
                    catch (Exception ex) when (!ct.IsCancellationRequested)
                    {
                        var failure = await RegisterSourceFailureAsync(consecutiveSourceFailures, options, ct).ConfigureAwait(false);
                        consecutiveSourceFailures = failure.Failures;
                        if (failure.BudgetExceeded)
                        {
                            return RunTerminal(
                                FollowerExitReason.FatalVerdict,
                                lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                                detail: $"source persistently unavailable ({DescribeException(ex)}) after {consecutiveSourceFailures} attempts");
                        }
                        continue;
                    }

                    var enumerator = stream.GetAsyncEnumerator(ct);
                    try
                    {
                        while (true)
                        {
                            bool hasNext;
                            try
                            {
                                hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
                            }
                            catch (Exception ex) when (!ct.IsCancellationRequested)
                            {
                                var failure = await RegisterSourceFailureAsync(consecutiveSourceFailures, options, ct).ConfigureAwait(false);
                                consecutiveSourceFailures = failure.Failures;
                                if (failure.BudgetExceeded)
                                {
                                    return RunTerminal(
                                        FollowerExitReason.FatalVerdict,
                                        lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                                        detail: $"source persistently failing ({DescribeException(ex)}) after {consecutiveSourceFailures} consecutive attempts");
                                }
                                restartLoop = true;
                                break;
                            }
                            if (!hasNext) break;
                            consecutiveSourceFailures = 0;
                            var blockBundle = enumerator.Current;
                            ct.ThrowIfCancellationRequested();

                            var header = blockBundle.Header;
                            var blockNumber = (ulong)header.BlockNumber;

                            var result = await ArmAndExecuteBlockAsync(bundle, executor, blockNumber, blockBundle, ct).ConfigureAwait(false);

                            var step = result.RootMatches
                                ? await HandleMatchedBlockAsync(
                                    bundle, canonical, policy, executorFactory, options, result, blockBundle, blockNumber,
                                    lastCommittedBlock, lastCommittedHash, currentStart,
                                    blocksExecuted, rootMismatches, consecutiveDivergences, rewindCyclesUsed, executor,
                                    ct, logger).ConfigureAwait(false)
                                : await HandleStateRootMismatchAsync(
                                    source, bundle, canonical, policy, executorFactory, options, result, blockNumber,
                                    lastCommittedBlock, lastCommittedHash, currentStart,
                                    blocksExecuted, rootMismatches, consecutiveDivergences, rewindCyclesUsed, executor,
                                    ct).ConfigureAwait(false);

                            lastCommittedBlock = step.LastCommittedBlock;
                            lastCommittedHash = step.LastCommittedHash;
                            currentStart = step.CurrentStart;
                            blocksExecuted = step.BlocksExecuted;
                            rootMismatches = step.RootMismatches;
                            consecutiveDivergences = step.ConsecutiveDivergences;
                            rewindCyclesUsed = step.RewindCyclesUsed;
                            executor = step.Executor;
                            if (step.Terminal is FollowerRunResult stepTerminal) return stepTerminal;
                            if (step.Signal == BlockStepSignal.RestartOuterLoop)
                            {
                                restartLoop = true;
                                break;
                            }
                        }
                    }
                    finally
                    {
                        await enumerator.DisposeAsync().ConfigureAwait(false);
                    }

                    if (!restartLoop)
                    {
                        var chainBreak = await HandleChainBreakAsync(
                            source, bundle, canonical, executorFactory, policy, options,
                            forkChoice, mempoolReconciler,
                            lastCommittedBlock, lastCommittedHash,
                            blocksExecuted, rootMismatches, rewindCyclesUsed,
                            ct, logger).ConfigureAwait(false);

                        if (chainBreak is RewindApplication cbRewind)
                        {
                            if (cbRewind.ResumeWithoutRewind)
                            {
                                restartLoop = true;
                            }
                            else
                            {
                                rewindCyclesUsed = cbRewind.RewindCyclesUsed;
                                lastCommittedBlock = cbRewind.LastCommittedBlock;
                                lastCommittedHash = cbRewind.LastCommittedHash;
                                if (cbRewind.Terminal is FollowerRunResult cbTerminal)
                                {
                                    return cbTerminal;
                                }

                                executor = cbRewind.Executor;
                                currentStart = cbRewind.CurrentStart;
                                restartLoop = true;
                            }
                        }
                    }

                    if (!restartLoop) break;
                }

                return RunTerminal(
                    FollowerExitReason.SourceCompleted,
                    lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                    detail: "source stream completed");
            }
            catch (OperationCanceledException)
            {
                return RunTerminal(
                    FollowerExitReason.Cancelled,
                    lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                    detail: "cancelled");
            }
            finally
            {
                await CommitCursorAtRunExitAsync(bundle, lastCommittedBlock, lastCommittedHash, logger)
                    .ConfigureAwait(false);
            }
        }

        private static async Task<(int Failures, bool BudgetExceeded)> RegisterSourceFailureAsync(
            int consecutiveSourceFailures, FollowerOptions options, CancellationToken ct)
        {
            var failures = consecutiveSourceFailures + 1;
            if (ExceededSourceFailureBudget(failures, options))
            {
                return (failures, true);
            }
            await Task.Delay(SourceRetryBackoff, ct).ConfigureAwait(false);
            return (failures, false);
        }

        private static async Task<BlockImporterResult> ArmAndExecuteBlockAsync(
            IChainStoreBundle bundle, IBlockExecutor executor, ulong blockNumber, BlockBundle blockBundle, CancellationToken ct)
        {
            (bundle as IAtomicBlockFlush)?.ArmWithdrawals(blockNumber, blockBundle.Withdrawals);
            return await executor.ProcessBlockAsync(
                blockBundle.Header,
                blockBundle.Transactions,
                blockBundle.Uncles,
                blockBundle.Withdrawals,
                ct).ConfigureAwait(false);
        }

        private enum BlockStepSignal { ContinueLoop, RestartOuterLoop }

        private readonly struct BlockStepOutcome
        {
            public BlockStepSignal Signal { get; init; }
            public FollowerRunResult Terminal { get; init; }
            public ulong LastCommittedBlock { get; init; }
            public byte[] LastCommittedHash { get; init; }
            public ulong CurrentStart { get; init; }
            public ulong BlocksExecuted { get; init; }
            public ulong RootMismatches { get; init; }
            public int ConsecutiveDivergences { get; init; }
            public ulong RewindCyclesUsed { get; init; }
            public IBlockExecutor Executor { get; init; }
        }

        private async Task<BlockStepOutcome> HandleMatchedBlockAsync(
            IChainStoreBundle bundle,
            ICanonicalStateRootSource canonical,
            IValidationPolicy policy,
            Func<IChainStoreBundle, IBlockExecutor> executorFactory,
            FollowerOptions options,
            BlockImporterResult result,
            BlockBundle blockBundle,
            ulong blockNumber,
            ulong lastCommittedBlock,
            byte[] lastCommittedHash,
            ulong currentStart,
            ulong blocksExecuted,
            ulong rootMismatches,
            int consecutiveDivergences,
            ulong rewindCyclesUsed,
            IBlockExecutor executor,
            CancellationToken ct,
            ILogger logger)
        {
            lastCommittedBlock = blockNumber;
            lastCommittedHash = blockBundle.HeaderHash;
            currentStart = blockNumber + 1;
            blocksExecuted++;
            consecutiveDivergences = 0;
            await CommitMatchedBlockAsync(
                bundle, blockNumber, blockBundle.HeaderHash, blockBundle,
                result.ComputedStateRoot, options, logger, ct).ConfigureAwait(false);

            if (canonical != null && policy.ShouldAnchorAt(lastCommittedBlock))
            {
                var anchor = await RunAnchorCheckAsync(
                    bundle, canonical, policy, executorFactory, options, result,
                    lastCommittedBlock, lastCommittedHash, blocksExecuted,
                    rootMismatches, consecutiveDivergences, rewindCyclesUsed,
                    ct, logger).ConfigureAwait(false);

                if (anchor.RewoundHeadHash != null)
                {
                    lastCommittedBlock = anchor.RewoundHeadBlock;
                    lastCommittedHash = anchor.RewoundHeadHash;
                }
                if (anchor.Terminal is FollowerRunResult anchorTerminal)
                {
                    return new BlockStepOutcome
                    {
                        Terminal = anchorTerminal,
                        LastCommittedBlock = lastCommittedBlock,
                        LastCommittedHash = lastCommittedHash,
                        CurrentStart = currentStart,
                        BlocksExecuted = blocksExecuted,
                        RootMismatches = rootMismatches,
                        ConsecutiveDivergences = consecutiveDivergences,
                        RewindCyclesUsed = rewindCyclesUsed,
                        Executor = executor,
                    };
                }

                rootMismatches = anchor.RootMismatches;
                consecutiveDivergences = anchor.ConsecutiveDivergences;
                rewindCyclesUsed = anchor.RewindCyclesUsed;
                if (anchor.Disposition == AnchorDisposition.RestartOuterLoop)
                {
                    return new BlockStepOutcome
                    {
                        Signal = BlockStepSignal.RestartOuterLoop,
                        LastCommittedBlock = lastCommittedBlock,
                        LastCommittedHash = lastCommittedHash,
                        CurrentStart = anchor.Rewound!.Value.CurrentStart,
                        BlocksExecuted = blocksExecuted,
                        RootMismatches = rootMismatches,
                        ConsecutiveDivergences = consecutiveDivergences,
                        RewindCyclesUsed = rewindCyclesUsed,
                        Executor = anchor.Rewound!.Value.Executor,
                    };
                }
            }

            if (options.EndBlock.HasValue && lastCommittedBlock >= options.EndBlock.Value)
            {
                return new BlockStepOutcome
                {
                    Terminal = RunTerminal(
                        FollowerExitReason.SourceCompleted,
                        lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                        detail: $"reached EndBlock={options.EndBlock.Value:N0}"),
                    LastCommittedBlock = lastCommittedBlock,
                    LastCommittedHash = lastCommittedHash,
                    CurrentStart = currentStart,
                    BlocksExecuted = blocksExecuted,
                    RootMismatches = rootMismatches,
                    ConsecutiveDivergences = consecutiveDivergences,
                    RewindCyclesUsed = rewindCyclesUsed,
                    Executor = executor,
                };
            }

            return new BlockStepOutcome
            {
                Signal = BlockStepSignal.ContinueLoop,
                LastCommittedBlock = lastCommittedBlock,
                LastCommittedHash = lastCommittedHash,
                CurrentStart = currentStart,
                BlocksExecuted = blocksExecuted,
                RootMismatches = rootMismatches,
                ConsecutiveDivergences = consecutiveDivergences,
                RewindCyclesUsed = rewindCyclesUsed,
                Executor = executor,
            };
        }

        private async Task<BlockStepOutcome> HandleStateRootMismatchAsync(
            IBlockSource source,
            IChainStoreBundle bundle,
            ICanonicalStateRootSource canonical,
            IValidationPolicy policy,
            Func<IChainStoreBundle, IBlockExecutor> executorFactory,
            FollowerOptions options,
            BlockImporterResult result,
            ulong blockNumber,
            ulong lastCommittedBlock,
            byte[] lastCommittedHash,
            ulong currentStart,
            ulong blocksExecuted,
            ulong rootMismatches,
            int consecutiveDivergences,
            ulong rewindCyclesUsed,
            IBlockExecutor executor,
            CancellationToken ct)
        {
            rootMismatches++;
            consecutiveDivergences++;
            if (ExceededDivergenceBudget(consecutiveDivergences, options))
            {
                return new BlockStepOutcome
                {
                    Terminal = RunTerminal(
                        FollowerExitReason.FatalVerdict,
                        lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                        detail: $"max consecutive divergences ({options.MaxConsecutiveDivergences}) exceeded at block {blockNumber:N0}"),
                    LastCommittedBlock = lastCommittedBlock,
                    LastCommittedHash = lastCommittedHash,
                    CurrentStart = currentStart,
                    BlocksExecuted = blocksExecuted,
                    RootMismatches = rootMismatches,
                    ConsecutiveDivergences = consecutiveDivergences,
                    RewindCyclesUsed = rewindCyclesUsed,
                    Executor = executor,
                };
            }

            var verdict = await DiagnoseMismatchVerdictAsync(canonical, blockNumber, result, ct).ConfigureAwait(false);

            var action = policy.OnVerdict(verdict, blockNumber);
            await source.ReportBadBundleAsync(blockNumber, BadBundleReason.StateRootMismatch, ct)
                .ConfigureAwait(false);

            switch (action)
            {
                case ValidationAction.Fatal:
                    return new BlockStepOutcome
                    {
                        Terminal = RunTerminal(
                            FollowerExitReason.FatalVerdict,
                            lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                            detail: $"fatal verdict at block {blockNumber:N0}: {verdict.Detail}"),
                        LastCommittedBlock = lastCommittedBlock,
                        LastCommittedHash = lastCommittedHash,
                        CurrentStart = currentStart,
                        BlocksExecuted = blocksExecuted,
                        RootMismatches = rootMismatches,
                        ConsecutiveDivergences = consecutiveDivergences,
                        RewindCyclesUsed = rewindCyclesUsed,
                        Executor = executor,
                    };

                case ValidationAction.RewindAndRetry:
                    var rewind = await ApplyValidatingRewindAsync(
                        bundle, canonical, executorFactory, options,
                        lastCommittedBlock, lastCommittedHash,
                        blocksExecuted, rootMismatches, rewindCyclesUsed,
                        ct).ConfigureAwait(false);

                    return new BlockStepOutcome
                    {
                        Signal = BlockStepSignal.RestartOuterLoop,
                        Terminal = rewind.Terminal,
                        LastCommittedBlock = rewind.LastCommittedBlock,
                        LastCommittedHash = rewind.LastCommittedHash,
                        CurrentStart = rewind.CurrentStart,
                        BlocksExecuted = blocksExecuted,
                        RootMismatches = rootMismatches,
                        ConsecutiveDivergences = consecutiveDivergences,
                        RewindCyclesUsed = rewind.RewindCyclesUsed,
                        Executor = rewind.Executor,
                    };
            }

            return new BlockStepOutcome
            {
                Signal = BlockStepSignal.ContinueLoop,
                LastCommittedBlock = lastCommittedBlock,
                LastCommittedHash = lastCommittedHash,
                CurrentStart = currentStart,
                BlocksExecuted = blocksExecuted,
                RootMismatches = rootMismatches,
                ConsecutiveDivergences = consecutiveDivergences,
                RewindCyclesUsed = rewindCyclesUsed,
                Executor = executor,
            };
        }

        private readonly struct RewindApplication
        {
            public FollowerRunResult Terminal { get; init; }
            public ulong RewindCyclesUsed { get; init; }
            public ulong LastCommittedBlock { get; init; }
            public byte[] LastCommittedHash { get; init; }
            public IBlockExecutor Executor { get; init; }
            public ulong CurrentStart { get; init; }
            public bool ResumeWithoutRewind { get; init; }
        }

        private async Task<RewindApplication> ApplyValidatingRewindAsync(
            IChainStoreBundle bundle,
            ICanonicalStateRootSource canonical,
            Func<IChainStoreBundle, IBlockExecutor> executorFactory,
            FollowerOptions options,
            ulong lastCommittedBlock,
            byte[] lastCommittedHash,
            ulong blocksExecuted,
            ulong rootMismatches,
            ulong rewindCyclesUsed,
            CancellationToken ct)
        {
            var outcome = await ReorgRewindCoordinator.ValidatingRewindAsync(
                bundle, canonical, options,
                lastCommittedBlock, lastCommittedHash,
                blocksExecuted, rootMismatches, rewindCyclesUsed,
                ct).ConfigureAwait(false);

            var (head, headHash) = HeadAfterRewind(outcome, lastCommittedHash);
            if (outcome.TerminalResult is FollowerRunResult terminal)
            {
                return new RewindApplication
                {
                    Terminal = terminal,
                    RewindCyclesUsed = outcome.RewindCyclesUsed,
                    LastCommittedBlock = head,
                    LastCommittedHash = headHash,
                };
            }

            var rewound = AdoptRewoundHead(outcome, executorFactory, bundle, headHash);
            return new RewindApplication
            {
                RewindCyclesUsed = outcome.RewindCyclesUsed,
                LastCommittedBlock = head,
                LastCommittedHash = headHash,
                Executor = rewound.Executor,
                CurrentStart = rewound.CurrentStart,
            };
        }

        private async Task<RewindApplication?> HandleChainBreakAsync(
            IBlockSource source,
            IChainStoreBundle bundle,
            ICanonicalStateRootSource canonical,
            Func<IChainStoreBundle, IBlockExecutor> executorFactory,
            IValidationPolicy policy,
            FollowerOptions options,
            IChainForkChoice forkChoice,
            IMempoolReorgReconciler mempoolReconciler,
            ulong lastCommittedBlock,
            byte[] lastCommittedHash,
            ulong blocksExecuted,
            ulong rootMismatches,
            ulong rewindCyclesUsed,
            CancellationToken ct,
            ILogger logger)
        {
            if (source.LastChainBreak is not { } cb)
            {
                return null;
            }

            if (forkChoice != null && cb.IncomingHeader != null)
            {
                var forkVerdict = await forkChoice
                    .ShouldAdoptAsync(cb.IncomingHeader, cb.IncomingHash, cb.SourcePeerNodeId, ct)
                    .ConfigureAwait(false);

                if (forkVerdict.Outcome == ForkChoiceOutcome.AdoptIncoming)
                {
                    return await ApplyForkChoiceAdoptionAsync(
                        bundle, executorFactory, mempoolReconciler, cb, forkVerdict.CommonAncestor,
                        lastCommittedBlock, lastCommittedHash, blocksExecuted, rootMismatches, rewindCyclesUsed,
                        ct, logger).ConfigureAwait(false);
                }

                if (forkVerdict.Outcome == ForkChoiceOutcome.KeepLocal)
                {
                    return new RewindApplication { ResumeWithoutRewind = true };
                }

                logger.LogInformation(
                    "sync.fork_choice.undecidable block={Block} reason={Reason}; falling back to policy",
                    cb.AtBlock, forkVerdict.Reason);
            }

            var cbVerdict = BuildChainBreakVerdict(cb);
            var cbAction = policy.OnVerdict(cbVerdict, cb.AtBlock);
            if (cbAction == ValidationAction.Fatal)
            {
                return new RewindApplication
                {
                    Terminal = RunTerminal(
                        FollowerExitReason.FatalVerdict,
                        lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                        detail: $"chain-break fatal at {cb.AtBlock:N0}: {cbVerdict.Detail}"),
                    RewindCyclesUsed = rewindCyclesUsed,
                    LastCommittedBlock = lastCommittedBlock,
                    LastCommittedHash = lastCommittedHash,
                };
            }
            if (cbAction == ValidationAction.RewindAndRetry)
            {
                return await ApplyValidatingRewindAsync(
                    bundle, canonical, executorFactory, options,
                    lastCommittedBlock, lastCommittedHash,
                    blocksExecuted, rootMismatches, rewindCyclesUsed,
                    ct).ConfigureAwait(false);
            }

            return null;
        }

        private async Task<RewindApplication> ApplyForkChoiceAdoptionAsync(
            IChainStoreBundle bundle,
            Func<IChainStoreBundle, IBlockExecutor> executorFactory,
            IMempoolReorgReconciler mempoolReconciler,
            DivergenceSignal cb,
            ulong commonAncestor,
            ulong lastCommittedBlock,
            byte[] lastCommittedHash,
            ulong blocksExecuted,
            ulong rootMismatches,
            ulong rewindCyclesUsed,
            CancellationToken ct,
            ILogger logger)
        {
            if (mempoolReconciler != null)
            {
                await mempoolReconciler.ReconcileAsync(
                    bundle, commonAncestor, lastCommittedBlock, cb.IncomingTransactions, ct).ConfigureAwait(false);
            }

            var outcome = await ReorgRewindCoordinator.TryRewindToForkChoiceAncestorAsync(
                bundle, commonAncestor, lastCommittedBlock, blocksExecuted, rootMismatches,
                ct, logger).ConfigureAwait(false);

            if (outcome.Terminal is FollowerRunResult terminal)
            {
                return new RewindApplication
                {
                    Terminal = terminal,
                    RewindCyclesUsed = rewindCyclesUsed + 1,
                    LastCommittedBlock = outcome.NewHead,
                    LastCommittedHash = outcome.NewHeadHash ?? lastCommittedHash,
                };
            }

            return new RewindApplication
            {
                RewindCyclesUsed = rewindCyclesUsed + 1,
                LastCommittedBlock = outcome.NewHead,
                LastCommittedHash = outcome.NewHeadHash ?? lastCommittedHash,
                Executor = executorFactory(bundle),
                CurrentStart = outcome.NewHead + 1,
            };
        }

        private readonly struct RewoundHead
        {
            public RewoundHead(
                IBlockExecutor executor, ulong currentStart, ulong lastCommittedBlock, byte[] lastCommittedHash)
            {
                Executor = executor;
                CurrentStart = currentStart;
                LastCommittedBlock = lastCommittedBlock;
                LastCommittedHash = lastCommittedHash;
            }

            public IBlockExecutor Executor { get; }
            public ulong CurrentStart { get; }
            public ulong LastCommittedBlock { get; }
            public byte[] LastCommittedHash { get; }
        }

        private static (ulong Block, byte[] Hash) HeadAfterRewind(
            in ReorgRewindCoordinator.RewindLoopOutcome outcome, byte[] currentLastCommittedHash)
            => (outcome.NewHead, outcome.NewHeadHash ?? currentLastCommittedHash);

        private static RewoundHead AdoptRewoundHead(
            in ReorgRewindCoordinator.RewindLoopOutcome outcome,
            Func<IChainStoreBundle, IBlockExecutor> executorFactory,
            IChainStoreBundle bundle,
            byte[] currentLastCommittedHash)
            => new RewoundHead(
                executorFactory(bundle),
                outcome.NewHead + 1,
                outcome.NewHead,
                outcome.NewHeadHash ?? currentLastCommittedHash);

        private enum AnchorDisposition { ContinueToEndBlockCheck, RestartOuterLoop }

        private readonly struct AnchorCheckDisposition
        {
            public AnchorDisposition Disposition { get; init; }
            public FollowerRunResult Terminal { get; init; }
            public ulong RootMismatches { get; init; }
            public int ConsecutiveDivergences { get; init; }
            public ulong RewindCyclesUsed { get; init; }
            public RewoundHead? Rewound { get; init; }

            public ulong RewoundHeadBlock { get; init; }
            public byte[] RewoundHeadHash { get; init; }
        }

        private async Task<AnchorCheckDisposition> RunAnchorCheckAsync(
            IChainStoreBundle bundle,
            ICanonicalStateRootSource canonical,
            IValidationPolicy policy,
            Func<IChainStoreBundle, IBlockExecutor> executorFactory,
            FollowerOptions options,
            BlockImporterResult result,
            ulong lastCommittedBlock,
            byte[] lastCommittedHash,
            ulong blocksExecuted,
            ulong rootMismatches,
            int consecutiveDivergences,
            ulong rewindCyclesUsed,
            CancellationToken ct,
            ILogger logger)
        {
            var anchorVerdict = await DiagnoseAnchorVerdictAsync(canonical, lastCommittedBlock, result, ct).ConfigureAwait(false);

            LogAnchorCheckOutcome(anchorVerdict, result, lastCommittedBlock, logger);

            if (ClassifyAnchorCheck(anchorVerdict, result) != AnchorCheckOutcome.Disagreed)
                return ContinueToEndBlockCheck(rootMismatches, consecutiveDivergences, rewindCyclesUsed);

            rootMismatches++;
            consecutiveDivergences++;
            if (ExceededDivergenceBudget(consecutiveDivergences, options))
            {
                return new AnchorCheckDisposition
                {
                    Terminal = RunTerminal(
                        FollowerExitReason.FatalVerdict,
                        lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                        detail: $"max consecutive divergences ({options.MaxConsecutiveDivergences}) " +
                                $"exceeded at periodic anchor check on block {lastCommittedBlock:N0}"),
                };
            }

            var anchorAction = policy.OnVerdict(anchorVerdict, lastCommittedBlock);
            switch (anchorAction)
            {
                case ValidationAction.Fatal:
                    return new AnchorCheckDisposition
                    {
                        Terminal = RunTerminal(
                            FollowerExitReason.FatalVerdict,
                            lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                            detail: $"periodic anchor fatal at block {lastCommittedBlock:N0}: {anchorVerdict.Detail}"),
                    };

                case ValidationAction.RewindAndRetry:
                    var anchorRewindOutcome = await ReorgRewindCoordinator.ValidatingRewindAsync(
                        bundle, canonical, options,
                        lastCommittedBlock, lastCommittedHash,
                        blocksExecuted, rootMismatches, rewindCyclesUsed,
                        ct).ConfigureAwait(false);

                    rewindCyclesUsed = anchorRewindOutcome.RewindCyclesUsed;
                    if (anchorRewindOutcome.TerminalResult is FollowerRunResult anchorTerminal)
                    {
                        var (haltedAt, haltedHash) = HeadAfterRewind(anchorRewindOutcome, lastCommittedHash);
                        return new AnchorCheckDisposition
                        {
                            Terminal = anchorTerminal,
                            RewindCyclesUsed = rewindCyclesUsed,
                            RewoundHeadBlock = haltedAt,
                            RewoundHeadHash = haltedHash,
                        };
                    }

                    return new AnchorCheckDisposition
                    {
                        Disposition = AnchorDisposition.RestartOuterLoop,
                        RootMismatches = rootMismatches,
                        ConsecutiveDivergences = consecutiveDivergences,
                        RewindCyclesUsed = rewindCyclesUsed,
                        Rewound = AdoptRewoundHead(anchorRewindOutcome, executorFactory, bundle, lastCommittedHash),
                    };
            }

            return ContinueToEndBlockCheck(rootMismatches, consecutiveDivergences, rewindCyclesUsed);
        }

        private static AnchorCheckDisposition ContinueToEndBlockCheck(
            ulong rootMismatches, int consecutiveDivergences, ulong rewindCyclesUsed)
            => new AnchorCheckDisposition
            {
                Disposition = AnchorDisposition.ContinueToEndBlockCheck,
                RootMismatches = rootMismatches,
                ConsecutiveDivergences = consecutiveDivergences,
                RewindCyclesUsed = rewindCyclesUsed,
            };

        private enum AnchorCheckOutcome { Agreed, Disagreed, NotChecked }

        private static AnchorCheckOutcome ClassifyAnchorCheck(DivergenceVerdict anchorVerdict, BlockImporterResult result)
            => anchorVerdict.Outcome == DivergenceOutcome.SourceUnavailable
                ? AnchorCheckOutcome.NotChecked
                : ByteUtil.AreEqual(anchorVerdict.CanonicalStateRoot, result.ComputedStateRoot)
                    ? AnchorCheckOutcome.Agreed
                    : AnchorCheckOutcome.Disagreed;

        private async Task<DivergenceVerdict> DiagnoseAnchorVerdictAsync(
            ICanonicalStateRootSource canonical, ulong lastCommittedBlock, BlockImporterResult result, CancellationToken ct)
        {
            DivergenceVerdict anchorVerdict;
            try
            {
                anchorVerdict = await canonical
                    .DiagnoseAsync(lastCommittedBlock, result.ComputedStateRoot, result.ComputedStateRoot, ct)
                    .ConfigureAwait(false);
            }
            catch (System.Exception ex)
            {
                anchorVerdict = new DivergenceVerdict(
                    DivergenceOutcome.SourceUnavailable, null, null, canonical.Name,
                    $"anchor source threw {DescribeException(ex)}");
            }
            return anchorVerdict;
        }

        private void LogAnchorCheckOutcome(
            DivergenceVerdict anchorVerdict, BlockImporterResult result, ulong lastCommittedBlock, ILogger logger)
        {
            switch (ClassifyAnchorCheck(anchorVerdict, result))
            {
                case AnchorCheckOutcome.Agreed:
                    logger.LogInformation(
                        "anchor check PASS: block={Block} source={Source}",
                        lastCommittedBlock, anchorVerdict.SourceName);
                    break;
                case AnchorCheckOutcome.NotChecked:
                    logger.LogDebug(
                        "anchor check skipped: block={Block} detail={Detail}",
                        lastCommittedBlock, anchorVerdict.Detail);
                    break;
            }
        }

        private async Task<DivergenceVerdict> DiagnoseMismatchVerdictAsync(
            ICanonicalStateRootSource canonical, ulong blockNumber, BlockImporterResult result, CancellationToken ct)
        {
            DivergenceVerdict verdict;
            if (canonical != null)
            {
                try
                {
                    verdict = await canonical
                        .DiagnoseAsync(blockNumber, result.ExpectedStateRoot, result.ComputedStateRoot, ct)
                        .ConfigureAwait(false);
                }
                catch (System.Exception ex)
                {
                    verdict = new DivergenceVerdict(
                        DivergenceOutcome.SourceUnavailable, null, null, canonical.Name,
                        $"canonical source threw {DescribeException(ex)}");
                }
            }
            else
            {
                verdict = new DivergenceVerdict(
                    DivergenceOutcome.SourceUnavailable, null, null, "<none>",
                    "no canonical source wired; state-root mismatch only");
            }
            return verdict;
        }

        private DivergenceVerdict BuildChainBreakVerdict(DivergenceSignal cb)
        {
            var cbVerdict = new DivergenceVerdict(
                DivergenceOutcome.SourceUnavailable,
                cb.PeerParentHash,
                cb.OurParentHash,
                cb.SourceName ?? "<source>",
                $"chain-break at {cb.AtBlock:N0}: {cb.QuorumPeerCount} peer(s) report parent " +
                $"0x{cb.PeerParentHash.ToHex().Substring(0, 16)}…; local parent " +
                $"0x{cb.OurParentHash.ToHex().Substring(0, 16)}…");
            return cbVerdict;
        }
    }
}
