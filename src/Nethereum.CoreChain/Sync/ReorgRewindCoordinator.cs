using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Sync
{
    internal static class ReorgRewindCoordinator
    {
        internal readonly struct ForwardRewindOutcome
        {
            public ForwardRewindOutcome(ulong newHead, byte[] newHeadHash, FollowerRunResult terminal)
            {
                NewHead = newHead;
                NewHeadHash = newHeadHash;
                Terminal = terminal;
            }
            public ulong NewHead { get; }
            public byte[] NewHeadHash { get; }
            public FollowerRunResult Terminal { get; }
        }

        internal static async Task SeedExecutedHashesAsync(
            IChainStoreBundle bundle, ulong head, byte[] headHash, ulong depth)
        {
            if (headHash == null || head == 0) return;
            if (bundle.Metadata.GetExecutedHash(head) == null) bundle.Metadata.SaveExecutedHash(head, headHash);

            var lowest = head > depth ? head - depth : 0UL;
            for (var n = head - 1; n >= lowest; n--)
            {
                if (bundle.Metadata.GetExecutedHash(n) == null)
                {
                    var canonical = await bundle.Blocks.GetHashByNumberAsync(new BigInteger(n)).ConfigureAwait(false);
                    if (canonical != null) bundle.Metadata.SaveExecutedHash(n, canonical);
                }
                if (n == 0) break;
            }
        }

        internal readonly struct AncestorRewindOutcome
        {
            public AncestorRewindOutcome(RewindResult rewind, FollowerRunResult terminal)
            {
                Rewind = rewind;
                Terminal = terminal;
            }
            public RewindResult Rewind { get; }
            public FollowerRunResult Terminal { get; }
        }

        internal static async Task<AncestorRewindOutcome> RewindToAncestorAsync(
            IChainStoreBundle bundle,
            IBlockExecutor executor,
            ulong ancestor,
            RewindScope scope,
            ulong lastCommittedBlock,
            ulong blocksExecuted,
            ulong rootMismatches,
            ILogger logger,
            CancellationToken ct)
        {
            var coordinator = new RewindCoordinator(bundle);
            RewindToAncestorResult result;
            try
            {
                result = executor is IRewindableExecutor rewindable
                    ? await rewindable.RewindToAncestorAsync(coordinator, ancestor, scope, ct).ConfigureAwait(false)
                    : await AncestorRewind.RunAsync(coordinator, ancestor, scope, null, logger, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "reorg.rewind_failed ancestor={Ancestor}", ancestor);
                return new AncestorRewindOutcome(null, Terminal(
                    FollowerExitReason.FatalVerdict, lastCommittedBlock, blocksExecuted, rootMismatches,
                    $"rewind to ancestor {ancestor:N0} threw {ex.GetType().Name}: {ex.Message}"));
            }

            if (result.Rewound) return new AncestorRewindOutcome(result.Rewind, null);

            if (result.Rewind.Outcome == RewindOutcome.SnapshotUsed)
                return new AncestorRewindOutcome(result.Rewind, new FollowerRunResult(
                    FollowerExitReason.SnapshotRestoreRequested,
                    lastCommittedBlock, blocksExecuted, rootMismatches, RewindCyclesUsed: 1,
                    SnapshotRestoreTarget: result.Rewind.RestoredCheckpoint,
                    Detail: result.Rewind.Detail));

            return new AncestorRewindOutcome(result.Rewind, Terminal(
                FollowerExitReason.RewindUnavailable, lastCommittedBlock, blocksExecuted, rootMismatches,
                $"rewind to ancestor {ancestor:N0} unavailable: {result.Rewind.Detail}"));
        }

        private static FollowerRunResult Terminal(
            FollowerExitReason reason, ulong lastCommittedBlock, ulong blocksExecuted, ulong rootMismatches, string detail)
            => new FollowerRunResult(
                reason, lastCommittedBlock, blocksExecuted, rootMismatches, RewindCyclesUsed: 0,
                SnapshotRestoreTarget: null, Detail: detail);

        internal static async Task<ForwardRewindOutcome?> TryRewindOnExternalReorgAsync(
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
            if (!await CommittedHeadWasRelaidBeneathAsync(bundle, lastCommittedBlock, lastCommittedHash)
                    .ConfigureAwait(false))
            {
                return null;
            }

            logger.LogWarning(
                "snap.tip.external_reorg committed={Block} - canonical store re-laid beneath the committed head; rewinding to the common ancestor",
                lastCommittedBlock);

            var depth = System.Math.Min(options.MaxReorgDepth, ExecutedHashRetention.Blocks);
            var ancestor = await FindCommonAncestorAsync(bundle, lastCommittedBlock, lastCommittedHash, depth).ConfigureAwait(false)
                           ?? await DisasterRecoveryTargetAsync(bundle, lastCommittedBlock, depth, logger).ConfigureAwait(false);
            var scope = options.ExternalHeaderFollow ? RewindScope.ExecutionOnly : RewindScope.ExecutionAndChain;

            var outcome = await RewindToAncestorAsync(
                bundle, executor, ancestor, scope, lastCommittedBlock, blocksExecuted, rootMismatches, logger, ct)
                .ConfigureAwait(false);
            if (outcome.Terminal != null)
                return new ForwardRewindOutcome(
                    bundle.Metadata.GetLastBlock(), bundle.Metadata.GetLastBlockHash(), outcome.Terminal);

            var newHead = bundle.Metadata.GetLastBlock();
            logger.LogInformation(
                "snap.tip.external_reorg_rewound new_head={Head} undone={Undone} via={Via}",
                newHead, outcome.Rewind.UndoneCount, outcome.Rewind.Outcome);
            return new ForwardRewindOutcome(newHead, bundle.Metadata.GetLastBlockHash(), terminal: null);
        }

        private static ForwardRewindOutcome FatalOutcome(
            ulong lastCommittedBlock, ulong blocksExecuted, ulong rootMismatches,
            FollowerExitReason reason, string detail)
            => new ForwardRewindOutcome(
                lastCommittedBlock, null,
                Terminal(reason, lastCommittedBlock, blocksExecuted, rootMismatches, detail));

        private static async Task<bool> CommittedHeadWasRelaidBeneathAsync(
            IChainStoreBundle bundle, ulong lastCommittedBlock, byte[] lastCommittedHash)
        {
            if (lastCommittedBlock == 0 || lastCommittedHash == null) return false;

            var storedHead = await bundle.Blocks
                .GetHashByNumberAsync(new BigInteger(lastCommittedBlock))
                .ConfigureAwait(false);
            if (storedHead == null || ByteUtil.AreEqual(storedHead, lastCommittedHash)) return false;

            return true;
        }

        internal static async Task<ulong?> FindCommonAncestorAsync(
            IChainStoreBundle bundle, ulong lastCommittedBlock, byte[] lastCommittedHash, ulong depth)
        {
            var lowest = lastCommittedBlock > depth ? lastCommittedBlock - depth : 0UL;
            for (var n = lastCommittedBlock; ; n--)
            {
                if (n == 0) return 0UL;
                var executedHash = n == lastCommittedBlock ? lastCommittedHash : bundle.Metadata.GetExecutedHash(n);
                if (executedHash != null)
                {
                    var canonicalHash = await bundle.Blocks.GetHashByNumberAsync(new BigInteger(n)).ConfigureAwait(false);
                    if (canonicalHash != null && ByteUtil.AreEqual(canonicalHash, executedHash)) return n;
                }
                if (n == lowest) return null;
            }
        }

        private static Task<ulong> DisasterRecoveryTargetAsync(
            IChainStoreBundle bundle, ulong lastCommittedBlock, ulong depth, ILogger logger)
        {
            var target = lastCommittedBlock > depth ? lastCommittedBlock - depth : 0UL;
            logger.LogError(
                "reorg.disaster_recovery committed={Committed} - no executed-hash record reaches a common ancestor within {Depth} blocks; falling back to the newest checkpoint at or below {Target}",
                lastCommittedBlock, depth, target);
            return Task.FromResult(target);
        }

        internal static async Task<RewindLoopOutcome> ValidatingRewindAsync(
            IChainStoreBundle bundle,
            ICanonicalStateRootSource canonical,
            FollowerOptions options,
            ulong lastCommittedBlock,
            byte[] lastCommittedHash,
            ulong blocksExecuted,
            ulong rootMismatches,
            ulong rewindCyclesUsed,
            CancellationToken ct,
            AncestorResolverDelegate ancestorResolver = null,
            ulong divergedBlock = 0UL,
            ILogger logger = null,
            RewindScope scope = RewindScope.ExecutionAndChain)
        {
            logger ??= NullLogger.Instance;
            var coordinator = new RewindCoordinator(bundle);
            ulong currentHead = lastCommittedBlock;
            byte[] currentHash = lastCommittedHash;
            bool fallBackToSingleShot = canonical == null;

            while (true)
            {
                rewindCyclesUsed++;
                if (rewindCyclesUsed > (ulong)options.MaxRewindCycles)
                {
                    return RewindLoopOutcome.Terminal(
                        rewindCyclesUsed, bundle.Metadata.GetLastBlock(),
                        bundle.Metadata.GetLastBlockHash() ?? currentHash,
                        BuildMaxRewindFatal(lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed, options, currentHead));
                }

                if (!fallBackToSingleShot)
                {
                    var verdict = await ConfirmCanonicalAtHeadAsync(bundle, canonical, currentHead, ct)
                        .ConfigureAwait(false);
                    if (verdict.SourceFailed)
                    {
                        fallBackToSingleShot = true;
                    }
                    else if (verdict.Match)
                    {
                        return RewindLoopOutcome.Resume(rewindCyclesUsed, currentHead, currentHash);
                    }
                    else if (currentHead == 0)
                    {
                        return RewindLoopOutcome.Terminal(
                            rewindCyclesUsed, bundle.Metadata.GetLastBlock(),
                            bundle.Metadata.GetLastBlockHash() ?? currentHash,
                            BuildBlockZeroDisagreementFatal(
                                lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed, verdict, canonical));
                    }
                }

                ulong rewindTarget = currentHead > 0 ? currentHead - 1 : 0;
                if (fallBackToSingleShot)
                    rewindTarget = await ResolveAncestorRewindTargetAsync(
                        rewindTarget, currentHead, divergedBlock, options, ancestorResolver, logger, ct)
                        .ConfigureAwait(false);
                var rewindResult = await coordinator.RewindToAsync(
                    rewindTarget, RewindPolicy.JournalFirstThenSnapshot, scope, ct).ConfigureAwait(false);

                switch (rewindResult.Outcome)
                {
                    case RewindOutcome.JournalUsed:
                    case RewindOutcome.NodeHistoryUsed:
                        currentHead = rewindResult.NewHead;
                        currentHash = bundle.Metadata.GetLastBlockHash() ?? currentHash;
                        if (fallBackToSingleShot)
                        {
                            return RewindLoopOutcome.Resume(rewindCyclesUsed, currentHead, currentHash);
                        }
                        continue;

                    case RewindOutcome.SnapshotUsed:
                        return RewindLoopOutcome.Terminal(
                            rewindCyclesUsed, bundle.Metadata.GetLastBlock(),
                            bundle.Metadata.GetLastBlockHash() ?? currentHash,
                            new FollowerRunResult(
                                FollowerExitReason.SnapshotRestoreRequested,
                                lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                                SnapshotRestoreTarget: rewindResult.RestoredCheckpoint,
                                Detail: rewindResult.Detail));

                    case RewindOutcome.NoOp:
                    case RewindOutcome.NoPathAvailable:
                    default:
                        return RewindLoopOutcome.Terminal(
                            rewindCyclesUsed, bundle.Metadata.GetLastBlock(),
                            bundle.Metadata.GetLastBlockHash() ?? currentHash,
                            new FollowerRunResult(
                                FollowerExitReason.RewindUnavailable,
                                lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                                SnapshotRestoreTarget: null,
                                Detail: rewindResult.Detail));
                }
            }
        }

        private static async Task<ulong> ResolveAncestorRewindTargetAsync(
            ulong target,
            ulong lastCommittedBlock,
            ulong divergedBlock,
            FollowerOptions options,
            AncestorResolverDelegate ancestorResolver,
            ILogger logger,
            CancellationToken ct)
        {
            if (ancestorResolver == null || lastCommittedBlock == 0) return target;

            ulong floor = lastCommittedBlock > options.MaxReorgDepth
                ? lastCommittedBlock - options.MaxReorgDepth
                : 0UL;
            try
            {
                var ancestor = await ancestorResolver(divergedBlock, floor, ct).ConfigureAwait(false);
                if (ancestor < lastCommittedBlock)
                {
                    target = ancestor;
                    logger.LogWarning(
                        "snap.tip.root_mismatch block={Block}; ancestor_resolver target={Target} (floor={Floor})",
                        divergedBlock, target, floor);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "snap.tip.ancestor_resolver_failed block={Block}; falling back to single-step rewind",
                    divergedBlock);
            }
            return target;
        }

        internal static FollowerRunResult BuildMaxRewindFatal(
            ulong lastCommittedBlock, ulong blocksExecuted, ulong rootMismatches,
            ulong rewindCyclesUsed, FollowerOptions options, ulong currentHead)
            => new FollowerRunResult(
                FollowerExitReason.FatalVerdict,
                lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                SnapshotRestoreTarget: null,
                Detail: $"exceeded MaxRewindCycles ({options.MaxRewindCycles}) " +
                        $"during validating rewind at head {currentHead:N0}");

        private static FollowerRunResult BuildBlockZeroDisagreementFatal(
            ulong lastCommittedBlock, ulong blocksExecuted, ulong rootMismatches,
            ulong rewindCyclesUsed, CanonicalVerdict verdict, ICanonicalStateRootSource canonical)
            => new FollowerRunResult(
                FollowerExitReason.FatalVerdict,
                lastCommittedBlock, blocksExecuted, rootMismatches, rewindCyclesUsed,
                SnapshotRestoreTarget: null,
                Detail: $"validating rewind reached block 0 with state root " +
                        $"0x{(verdict.OurRoot ?? System.Array.Empty<byte>()).ToHex()} that disagrees with " +
                        $"canonical 0x{(verdict.CanonicalRoot ?? System.Array.Empty<byte>()).ToHex()} via {canonical.Name}");

        internal readonly struct CanonicalVerdict
        {
            public bool Match { get; }
            public bool SourceFailed { get; }
            public byte[] OurRoot { get; }
            public byte[] CanonicalRoot { get; }
            public CanonicalVerdict(bool match, bool sourceFailed, byte[] ourRoot, byte[] canonicalRoot)
            {
                Match = match;
                SourceFailed = sourceFailed;
                OurRoot = ourRoot;
                CanonicalRoot = canonicalRoot;
            }
        }

        internal static async Task<CanonicalVerdict> ConfirmCanonicalAtHeadAsync(
            IChainStoreBundle bundle,
            ICanonicalStateRootSource canonical,
            ulong head,
            CancellationToken ct)
        {
            var oursAtHead = await ReadOurStateRootAtAsync(bundle, head, ct).ConfigureAwait(false);
            if (oursAtHead == null)
            {
                return new CanonicalVerdict(match: false, sourceFailed: true, null, null);
            }

            byte[] canonicalRoot;
            try
            {
                var (root, _) = await canonical.GetCanonicalAsync(head, ct).ConfigureAwait(false);
                canonicalRoot = root;
            }
            catch
            {
                return new CanonicalVerdict(match: false, sourceFailed: true, oursAtHead, null);
            }

            if (canonicalRoot == null)
            {
                return new CanonicalVerdict(match: false, sourceFailed: true, oursAtHead, null);
            }

            return new CanonicalVerdict(
                match: ByteUtil.AreEqual(oursAtHead, canonicalRoot),
                sourceFailed: false,
                oursAtHead,
                canonicalRoot);
        }

        internal static async Task<byte[]> ReadOurStateRootAtAsync(
            IChainStoreBundle bundle, ulong blockNumber, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var header = await bundle.Blocks.GetByNumberAsync(blockNumber).ConfigureAwait(false);
            return header?.StateRoot;
        }

        internal readonly struct RewindLoopOutcome
        {
            public ulong RewindCyclesUsed { get; }
            public ulong NewHead { get; }
            public byte[] NewHeadHash { get; }
            public FollowerRunResult TerminalResult { get; }

            private RewindLoopOutcome(
                ulong rewindCyclesUsed,
                ulong newHead,
                byte[] newHeadHash,
                FollowerRunResult terminalResult)
            {
                RewindCyclesUsed = rewindCyclesUsed;
                NewHead = newHead;
                NewHeadHash = newHeadHash;
                TerminalResult = terminalResult;
            }

            public static RewindLoopOutcome Resume(
                ulong rewindCyclesUsed, ulong newHead, byte[] newHeadHash)
                => new RewindLoopOutcome(rewindCyclesUsed, newHead, newHeadHash, null);

            public static RewindLoopOutcome Terminal(
                ulong rewindCyclesUsed, ulong newHead, byte[] newHeadHash, FollowerRunResult terminalResult)
                => new RewindLoopOutcome(rewindCyclesUsed, newHead, newHeadHash, terminalResult);
        }
    }
}
