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

        internal static async Task<ForwardRewindOutcome?> TryRewindOnExternalReorgAsync(
            IChainStoreBundle bundle,
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
                "snap.tip.external_reorg committed={Block} — canonical store re-laid beneath the committed head; rewinding to a proven checkpoint",
                lastCommittedBlock);

            var target = await FindNewestCheckpointMatchingCanonicalStoreAsync(bundle, lastCommittedBlock)
                .ConfigureAwait(false);
            if (target == null)
            {
                return NoProvenCheckpointFatal(lastCommittedBlock, blocksExecuted, rootMismatches);
            }

            var coordinator = new RewindCoordinator(bundle);
            RewindResult rewindResult;
            try
            {
                rewindResult = await coordinator
                    .RewindToAsync(target.Value, RewindPolicy.JournalFirstThenSnapshot, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "snap.tip.external_reorg_rewind_failed target={Target}", target);
                return new ForwardRewindOutcome(
                    bundle.Metadata.GetLastBlock(), bundle.Metadata.GetLastBlockHash(),
                    new FollowerRunResult(
                        FollowerExitReason.FatalVerdict,
                        lastCommittedBlock, blocksExecuted, rootMismatches, RewindCyclesUsed: 0,
                        SnapshotRestoreTarget: null,
                        Detail: $"external-reorg rewind threw {ex.GetType().Name}: {ex.Message}"));
            }

            return ApplyExternalReorgRewindResult(
                bundle, rewindResult, target.Value, lastCommittedBlock, blocksExecuted, rootMismatches, logger);
        }

        internal static async Task<ForwardRewindOutcome> TryRewindToForkChoiceAncestorAsync(
            IChainStoreBundle bundle,
            ulong commonAncestor,
            ulong lastCommittedBlock,
            ulong blocksExecuted,
            ulong rootMismatches,
            CancellationToken ct,
            ILogger logger)
        {
            logger.LogWarning(
                "sync.fork_choice.adopt_incoming committed={Block} ancestor={Ancestor} — a heavier/authoritative " +
                "competing branch was chosen; rewinding to the common ancestor",
                lastCommittedBlock, commonAncestor);

            var coordinator = new RewindCoordinator(bundle);
            RewindResult rewindResult;
            try
            {
                rewindResult = await coordinator
                    .RewindToAsync(commonAncestor, RewindPolicy.JournalFirstThenSnapshot, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "sync.fork_choice.rewind_failed target={Target}", commonAncestor);
                return new ForwardRewindOutcome(
                    bundle.Metadata.GetLastBlock(), bundle.Metadata.GetLastBlockHash(),
                    new FollowerRunResult(
                        FollowerExitReason.FatalVerdict,
                        lastCommittedBlock, blocksExecuted, rootMismatches, RewindCyclesUsed: 0,
                        SnapshotRestoreTarget: null,
                        Detail: $"fork-choice rewind to {commonAncestor:N0} threw {ex.GetType().Name}: {ex.Message}"));
            }

            return ApplyExternalReorgRewindResult(
                bundle, rewindResult, commonAncestor, lastCommittedBlock, blocksExecuted, rootMismatches, logger);
        }

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

        private static async Task<ulong?> FindNewestCheckpointMatchingCanonicalStoreAsync(
            IChainStoreBundle bundle, ulong lastCommittedBlock)
        {
            ulong? target = null;
            var checkpointBlocks = new List<ulong>(bundle.Metadata.ListCheckpointBlockNumbers());
            checkpointBlocks.Sort();
            for (int i = checkpointBlocks.Count - 1; i >= 0; i--)
            {
                var cpBlock = checkpointBlocks[i];
                if (cpBlock > lastCommittedBlock) continue;
                var cp = bundle.Metadata.GetCheckpoint(cpBlock);
                if (cp == null || cp.Value.BlockHash == null) continue;
                var storedAtCp = await bundle.Blocks
                    .GetHashByNumberAsync(new BigInteger(cpBlock))
                    .ConfigureAwait(false);
                if (storedAtCp != null && ByteUtil.AreEqual(storedAtCp, cp.Value.BlockHash))
                {
                    target = cpBlock;
                    break;
                }
            }
            return target;
        }

        private static ForwardRewindOutcome NoProvenCheckpointFatal(
            ulong lastCommittedBlock, ulong blocksExecuted, ulong rootMismatches)
            => new ForwardRewindOutcome(
                lastCommittedBlock, null,
                new FollowerRunResult(
                    FollowerExitReason.FatalVerdict,
                    lastCommittedBlock, blocksExecuted, rootMismatches, RewindCyclesUsed: 0,
                    SnapshotRestoreTarget: null,
                    Detail: $"reorg re-laid the canonical store beneath the committed head {lastCommittedBlock:N0} " +
                            "and no retained checkpoint matches the repaired chain — manual rewind required"));

        private static ForwardRewindOutcome ApplyExternalReorgRewindResult(
            IChainStoreBundle bundle,
            RewindResult rewindResult,
            ulong target,
            ulong lastCommittedBlock,
            ulong blocksExecuted,
            ulong rootMismatches,
            ILogger logger)
        {
            switch (rewindResult.Outcome)
            {
                case RewindOutcome.JournalUsed:
                case RewindOutcome.NodeHistoryUsed:
                    bundle.Metadata.DeleteCheckpointsAbove(target);
                    var newHead = bundle.Metadata.GetLastBlock();
                    var newHeadHash = bundle.Metadata.GetLastBlockHash();
                    logger.LogInformation(
                        "snap.tip.external_reorg_rewound new_head={Head} undone={Undone} via={Via}",
                        newHead, rewindResult.UndoneCount, rewindResult.Outcome);
                    return new ForwardRewindOutcome(newHead, newHeadHash, terminal: null);

                case RewindOutcome.SnapshotUsed:
                    return new ForwardRewindOutcome(
                        bundle.Metadata.GetLastBlock(), bundle.Metadata.GetLastBlockHash(),
                        new FollowerRunResult(
                            FollowerExitReason.SnapshotRestoreRequested,
                            lastCommittedBlock, blocksExecuted, rootMismatches, RewindCyclesUsed: 1,
                            SnapshotRestoreTarget: rewindResult.RestoredCheckpoint,
                            Detail: rewindResult.Detail));

                default:
                    return new ForwardRewindOutcome(
                        bundle.Metadata.GetLastBlock(), bundle.Metadata.GetLastBlockHash(),
                        new FollowerRunResult(
                            FollowerExitReason.RewindUnavailable,
                            lastCommittedBlock, blocksExecuted, rootMismatches, RewindCyclesUsed: 0,
                            SnapshotRestoreTarget: null,
                            Detail: $"external reorg at committed head {lastCommittedBlock:N0}: rewind unavailable: {rewindResult.Detail}"));
            }
        }

        internal static async Task<ForwardRewindOutcome> TryRewindOnRootMismatchAsync(
            IChainStoreBundle bundle,
            ulong lastCommittedBlock,
            ulong divergedBlock,
            FollowerOptions options,
            AncestorResolverDelegate ancestorResolver,
            ILogger logger,
            CancellationToken ct)
        {
            if (lastCommittedBlock == 0)
            {
                return NoRewindTargetAboveGenesisFatal(lastCommittedBlock, divergedBlock);
            }

            ulong singleStepTarget = lastCommittedBlock > 0 ? lastCommittedBlock - 1 : 0;
            ulong target = await ResolveAncestorRewindTargetAsync(
                    singleStepTarget, lastCommittedBlock, divergedBlock, options, ancestorResolver, logger, ct)
                .ConfigureAwait(false);

            logger.LogWarning(
                "snap.tip.root_mismatch block={Block}; attempting rewind to {Target}",
                divergedBlock, target);

            var coordinator = new RewindCoordinator(bundle);
            RewindResult rewindResult;
            try
            {
                rewindResult = await coordinator
                    .RewindToAsync(target, RewindPolicy.JournalFirstThenSnapshot, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "snap.tip.rewind_failed block={Block} target={Target}",
                    divergedBlock, target);
                return new ForwardRewindOutcome(
                    bundle.Metadata.GetLastBlock(), bundle.Metadata.GetLastBlockHash(),
                    new FollowerRunResult(
                        FollowerExitReason.FatalVerdict,
                        lastCommittedBlock, BlocksExecuted: 0, RootMismatches: 0, RewindCyclesUsed: 0,
                        SnapshotRestoreTarget: null,
                        Detail: $"tip-driven forward exec: rewind threw {ex.GetType().Name}: {ex.Message}"));
            }

            return ApplyRootMismatchRewindResult(bundle, rewindResult, lastCommittedBlock, divergedBlock, logger);
        }

        private static ForwardRewindOutcome NoRewindTargetAboveGenesisFatal(
            ulong lastCommittedBlock, ulong divergedBlock)
            => new ForwardRewindOutcome(
                lastCommittedBlock, null,
                new FollowerRunResult(
                    FollowerExitReason.FatalVerdict,
                    lastCommittedBlock, BlocksExecuted: 0, RootMismatches: 0, RewindCyclesUsed: 0,
                    SnapshotRestoreTarget: null,
                    Detail: $"tip-driven forward exec: state-root mismatch at block {divergedBlock:N0}; no rewind target above genesis"));

        private static async Task<ulong> ResolveAncestorRewindTargetAsync(
            ulong target,
            ulong lastCommittedBlock,
            ulong divergedBlock,
            FollowerOptions options,
            AncestorResolverDelegate ancestorResolver,
            ILogger logger,
            CancellationToken ct)
        {
            if (ancestorResolver != null && lastCommittedBlock > 0)
            {
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
            }
            return target;
        }

        private static ForwardRewindOutcome ApplyRootMismatchRewindResult(
            IChainStoreBundle bundle,
            RewindResult rewindResult,
            ulong lastCommittedBlock,
            ulong divergedBlock,
            ILogger logger)
        {
            switch (rewindResult.Outcome)
            {
                case RewindOutcome.JournalUsed:
                case RewindOutcome.NodeHistoryUsed:
                    var newHead = bundle.Metadata.GetLastBlock();
                    var newHeadHash = bundle.Metadata.GetLastBlockHash();
                    logger.LogInformation(
                        "snap.tip.rewound new_head={Head} undone={Undone} via={Via}",
                        newHead, rewindResult.UndoneCount, rewindResult.Outcome);
                    return new ForwardRewindOutcome(newHead, newHeadHash, terminal: null);

                case RewindOutcome.SnapshotUsed:
                    return new ForwardRewindOutcome(
                        bundle.Metadata.GetLastBlock(), bundle.Metadata.GetLastBlockHash(),
                        new FollowerRunResult(
                            FollowerExitReason.SnapshotRestoreRequested,
                            lastCommittedBlock, BlocksExecuted: 0, RootMismatches: 0, RewindCyclesUsed: 1,
                            SnapshotRestoreTarget: rewindResult.RestoredCheckpoint,
                            Detail: rewindResult.Detail));

                case RewindOutcome.NoOp:
                case RewindOutcome.NoPathAvailable:
                default:
                    return new ForwardRewindOutcome(
                        bundle.Metadata.GetLastBlock(), bundle.Metadata.GetLastBlockHash(),
                        new FollowerRunResult(
                            FollowerExitReason.RewindUnavailable,
                            lastCommittedBlock, BlocksExecuted: 0, RootMismatches: 0, RewindCyclesUsed: 0,
                            SnapshotRestoreTarget: null,
                            Detail: $"tip-driven forward exec: rewind unavailable at block {divergedBlock:N0}: {rewindResult.Detail}"));
            }
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
            CancellationToken ct)
        {
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
                var rewindResult = await coordinator.RewindToAsync(
                    rewindTarget, RewindPolicy.JournalFirstThenSnapshot, ct).ConfigureAwait(false);

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
