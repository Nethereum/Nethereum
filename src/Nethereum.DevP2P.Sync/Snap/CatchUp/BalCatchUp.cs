using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.Snap.CatchUp
{
    internal sealed class SnapSyncResetRequiredException : InvalidOperationException
    {
        public SnapSyncResetRequiredException(string reason, string message)
            : base(message)
        {
            Reason = reason;
        }

        public string Reason { get; }
    }

    internal sealed class BalCatchUp
    {
        internal const ulong MaxCatchUpBlocks = 90_000;
        internal const ulong WindowBlocks = 512;

        private readonly IChainStoreBundle _bundle;
        private readonly IFlatStateTrieGenerator _flatState;
        private readonly SnapBootstrapper.RollingPivot _rollingPivot;
        private readonly IBlockAccessListFetcher _fetcher;
        private readonly IBlockAccessListApplier _applier;
        private readonly SnapSyncMetrics _metrics;
        private readonly ILogger _logger;
        private SnapBootstrapper.PivotState _applied;

        internal BalCatchUp(
            IChainStoreBundle bundle,
            SnapBootstrapper.RollingPivot rollingPivot,
            SnapBootstrapper.PivotState applied,
            IBlockAccessListFetcher fetcher,
            IBlockAccessListApplier applier,
            SnapSyncMetrics metrics,
            ILogger logger)
        {
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _flatState = bundle as IFlatStateTrieGenerator
                ?? throw new ArgumentException("snap/2 catch-up needs a bundle that can prune flat state.", nameof(bundle));
            _rollingPivot = rollingPivot ?? throw new ArgumentNullException(nameof(rollingPivot));
            _applied = applied ?? throw new ArgumentNullException(nameof(applied));
            _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
            _applier = applier ?? throw new ArgumentNullException(nameof(applier));
            _metrics = metrics;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        internal static BalCatchUp Create(
            IChainStoreBundle bundle, IBlockAccessListPeerSource peers, SnapBootstrapper.RollingPivot rollingPivot,
            SnapBootstrapper.PivotState applied, SnapSyncMetrics metrics, ILogger logger,
            IBlockAccessListApplier applier = null)
            => new BalCatchUp(
                bundle, rollingPivot, applied,
                new BlockAccessListFetcher(peers, new BlockAccessListVerifier()),
                applier ?? new BlockAccessListApplier(bundle.State as ISnapFlatStateWriter, bundle.State),
                metrics, logger);

        public SnapBootstrapper.PivotState Applied => Volatile.Read(ref _applied);

        public async Task<byte[]> CatchUpAsync(IReadOnlyList<SnapSyncAccountTask> durableTasks, CancellationToken ct)
        {
            var target = _rollingPivot.Current;
            var applied = Applied;

            if (durableTasks == null)
            {
                Volatile.Write(ref _applied, target);
                return target.Header.StateRoot;
            }

            if (ByteUtil.AreEqual(target.Hash, applied.Hash))
            {
                _flatState.PruneFlatStateBeyond(durableTasks);
                return target.Header.StateRoot;
            }

            var from = (ulong)applied.Header.BlockNumber;
            var to = (ulong)target.Header.BlockNumber;
            if (to <= from)
                throw Reset("pivot_not_ahead", $"pivot {to} is not ahead of the applied pivot {from}");
            if (!await IsCanonicalAsync(applied).ConfigureAwait(false))
                throw Reset("pivot_reorged", $"applied pivot {from} 0x{applied.Hash.ToHex()} is no longer canonical");
            if (to - from > MaxCatchUpBlocks)
                throw Reset("gap_exceeds_bal_retention", $"gap {from + 1}..{to} exceeds the {MaxCatchUpBlocks}-block BAL retention");

            _flatState.PruneFlatStateBeyond(durableTasks);

            var frontier = new SnapTaskFrontier(durableTasks);
            for (var windowStart = from + 1; windowStart <= to; windowStart += WindowBlocks)
            {
                var windowEnd = Math.Min(windowStart + WindowBlocks - 1, to);
                await ApplyWindowAsync(windowStart, windowEnd, frontier, ct).ConfigureAwait(false);
            }

            if (!ByteUtil.AreEqual(Applied.Hash, target.Hash))
                throw new InvalidOperationException(
                    $"snap.bal_catchup: applied through block {to} 0x{Applied.Hash.ToHex()}, but the target pivot is 0x{target.Hash.ToHex()}.");

            _logger.LogInformation(
                "snap.bal_catchup.applied blocks={Count} range={From}..{To} new_root=0x{Root}",
                to - from, from + 1, to, target.Header.StateRoot.ToHex());
            return target.Header.StateRoot;
        }

        private async Task ApplyWindowAsync(ulong windowStart, ulong windowEnd, ISnapTaskFrontier frontier, CancellationToken ct)
        {
            var headers = new List<BlockHeader>();
            var hashes = new List<byte[]>();
            for (var number = windowStart; number <= windowEnd; number++)
            {
                var (header, hash) = await LoadCanonicalHeaderAsync(number).ConfigureAwait(false);
                headers.Add(header);
                hashes.Add(hash);
            }

            VerifyGapHeaderChain(Applied.Header, headers[^1], headers);

            var refs = new List<BalBlockRef>(headers.Count);
            for (var i = 0; i < headers.Count; i++)
            {
                if (headers[i].BlockAccessListHash == null)
                    throw new InvalidOperationException(
                        $"snap.bal_catchup: header at block {headers[i].BlockNumber} carries no BlockAccessListHash " +
                        "— BAL catch-up requires Amsterdam-onward headers (EIP-7928).");
                refs.Add(new BalBlockRef(hashes[i], headers[i].BlockAccessListHash));
            }

            var accessLists = await _fetcher.FetchAsync(refs, ct).ConfigureAwait(false);
            for (var i = 0; i < accessLists.Count; i++)
            {
                await _applier.ApplyAsync(accessLists[i], frontier, ct).ConfigureAwait(false);
                PersistAppliedPivot(headers[i], hashes[i]);
            }
            _metrics?.RecordBalHealBlocksApplied(accessLists.Count);
        }

        private async Task<(BlockHeader Header, byte[] Hash)> LoadCanonicalHeaderAsync(ulong number)
        {
            var hash = await _bundle.Blocks.GetHashByNumberAsync(new BigInteger(number)).ConfigureAwait(false);
            var header = hash == null ? null : await _bundle.Blocks.GetByHashAsync(hash).ConfigureAwait(false);
            if (header == null)
                throw new InvalidOperationException(
                    $"snap.bal_catchup: canonical header {number} is missing from the local chain; retrying once the header chain has it.");
            return (header, hash);
        }

        private void PersistAppliedPivot(BlockHeader header, byte[] hash)
        {
            var saved = _bundle.Metadata.GetSnapSyncState()
                ?? throw new InvalidOperationException("snap.bal_catchup: no persisted snap/2 state to advance.");
            _bundle.Metadata.SaveSnapSyncState(saved with
            {
                PivotBlockNumber = (ulong)header.BlockNumber,
                PivotBlockHash = hash,
            });
            Volatile.Write(ref _applied, new SnapBootstrapper.PivotState(header, hash));
        }

        private async Task<bool> IsCanonicalAsync(SnapBootstrapper.PivotState pivot)
        {
            var canonical = await _bundle.Blocks.GetHashByNumberAsync(new BigInteger((ulong)pivot.Header.BlockNumber)).ConfigureAwait(false);
            return canonical != null && ByteUtil.AreEqual(canonical, pivot.Hash);
        }

        private SnapSyncResetRequiredException Reset(string reason, string detail)
        {
            _logger.LogWarning("snap.bal_catchup.reset_required reason={Reason} {Detail}", reason, detail);
            return new SnapSyncResetRequiredException(reason, $"snap.bal_catchup: reset required ({reason}): {detail}.");
        }

        internal static void VerifyGapHeaderChain(
            BlockHeader oldHeader, BlockHeader newHeader, IReadOnlyList<BlockHeader> headers)
        {
            var parentHash = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(oldHeader);
            foreach (var header in headers)
            {
                if (!ByteUtil.AreEqual(header.ParentHash, parentHash))
                    throw new InvalidOperationException(
                        $"gap header at block {header.BlockNumber} does not link to its parent " +
                        "on the chain from the old pivot; refusing BALs verified against an unlinked header.");
                parentHash = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header);
            }
            if (!ByteUtil.AreEqual(parentHash, RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(newHeader)))
                throw new InvalidOperationException(
                    $"gap headers do not end at the new pivot block {newHeader.BlockNumber}.");
        }
    }
}
