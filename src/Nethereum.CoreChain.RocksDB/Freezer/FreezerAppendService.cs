using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Nethereum.CoreChain.RocksDB.Composition;
using Nethereum.CoreChain.Storage;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.Freezer
{
    internal sealed class FreezerAppendService
    {
        internal const long FullImmutabilityThreshold = 90_000;

        private readonly Nethereum.Freezer.Freezer _freezerAppend;
        private readonly Nethereum.CoreChain.Freezer.Codecs.FreezerCodecSet _freezerCodecs;
        private readonly RocksDbManager _rocks;
        private readonly FreezerBackgroundIndexer _freezerIndexer;
        private readonly object _freezerAppendLock;

        private readonly System.Threading.Tasks.ParallelOptions _freezeEncodeParallelOptions;

        private readonly System.Diagnostics.Stopwatch _freezeStatsLog = System.Diagnostics.Stopwatch.StartNew();
        private long _freezeStatsBlocks;
        private double _freezeBuildMs, _freezeAppendMs, _freezeCommitMs;
        private static readonly TimeSpan FreezeStatsLogInterval = TimeSpan.FromSeconds(15);

        private static readonly byte[] EmptyBlockAccessListHash =
            Nethereum.Model.BlockAccessListRLPEncoder.Current.Hash(new List<Nethereum.Model.AccountChanges>());

        private Nethereum.Freezer.FreezerBatch _openBatch;
        private long _blocksSinceCommit;
        private readonly System.Diagnostics.Stopwatch _commitTimer = System.Diagnostics.Stopwatch.StartNew();
        private static readonly TimeSpan CommitBackstopInterval = TimeSpan.FromMinutes(10);

        internal FreezerAppendService(
            Nethereum.Freezer.Freezer freezerAppend,
            Nethereum.CoreChain.Freezer.Codecs.FreezerCodecSet freezerCodecs,
            RocksDbManager rocks,
            FreezerBackgroundIndexer freezerIndexer,
            object freezerAppendLock)
        {
            _freezerAppend = freezerAppend;
            _freezerCodecs = freezerCodecs;
            _rocks = rocks;
            _freezerIndexer = freezerIndexer;
            _freezerAppendLock = freezerAppendLock;
            _freezeEncodeParallelOptions = new System.Threading.Tasks.ParallelOptions
            {
                MaxDegreeOfParallelism = Nethereum.Freezer.FrozenParallelism.Resolve(rocks.Options.FreezerBackgroundDegreeOfParallelism)
            };
        }

        internal long Head => _freezerAppend?.Items ?? 0;

        internal T WithAppendLock<T>(Func<T> op)
        {
            lock (_freezerAppendLock) return op();
        }

        internal void WithAppendLock(Action op)
        {
            lock (_freezerAppendLock) op();
        }

        internal int AppendFreezeEligiblePrefix(
            IReadOnlyList<PersistableBlock> blocks, System.Numerics.BigInteger freezeBoundary, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var splitIndex = 0;
            while (splitIndex < blocks.Count && blocks[splitIndex].Header.BlockNumber.ToBigInteger() <= freezeBoundary)
                splitIndex++;

            if (splitIndex == 0) return 0;

            var crossesBoundary = splitIndex < blocks.Count;

            int startIndex, committed;
            lock (_freezerAppendLock)
            {
                (startIndex, committed) = AppendToFreezer(blocks, splitIndex);
                if (crossesBoundary) CommitOpenBatch();
            }

            if (committed > 0 && !_freezerIndexer.IsRunning)
            {
                _freezerIndexer.IndexFrozenInline(blocks, startIndex, committed);
                _freezerIndexer.RenderFilterMaps();
            }

            return splitIndex;
        }

        internal System.Numerics.BigInteger ResolveFreezeBoundary() => ReadTipHeightStamp(_rocks) - FullImmutabilityThreshold;

        internal static System.Numerics.BigInteger ReadTipHeightStamp(RocksDbManager rocks)
        {
            rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            var heightKey = System.Text.Encoding.UTF8.GetBytes("height");
            var currentHeightRaw = rocks.Get(RocksDbManager.CF_METADATA, heightKey);
            return currentHeightRaw == null
                ? System.Numerics.BigInteger.MinusOne
                : Serialization.RocksDbSerializer.BytesToBigInteger(currentHeightRaw);
        }

        private (int startIndex, int committed) AppendToFreezer(IReadOnlyList<PersistableBlock> blocks, int freezableCount)
        {
            var freezerHead = _freezerAppend.Items;
            var startIndex = 0;
            while (startIndex < freezableCount && blocks[startIndex].Header.BlockNumber.ToBigInteger() < freezerHead)
                startIndex++;

            if (startIndex == freezableCount)
                return (startIndex, 0);

            var count = freezableCount - startIndex;
            var numbers = new long[count];
            var encoded = new Nethereum.Freezer.EncodedFrozenCluster[count];

            for (var i = startIndex; i < freezableCount; i++)
            {
                RequireBalActivationCompatible(blocks[i].Header);
                numbers[i - startIndex] = (long)blocks[i].Header.BlockNumber.ToBigInteger();
            }
            var swEncode = System.Diagnostics.Stopwatch.StartNew();
            System.Threading.Tasks.Parallel.For(0, count, _freezeEncodeParallelOptions,
                i => encoded[i] = _freezerAppend.Encode(BuildFrozenCluster(blocks[startIndex + i])));
            _freezeBuildMs += swEncode.Elapsed.TotalMilliseconds;

            _openBatch ??= _freezerAppend.BeginBatch();
            try
            {
                var swAppend = System.Diagnostics.Stopwatch.StartNew();
                for (var i = 0; i < count; i++)
                    _openBatch.AppendEncodedCluster(numbers[i], encoded[i]);
                _freezeAppendMs += swAppend.Elapsed.TotalMilliseconds;

                _blocksSinceCommit += count;
                if (_blocksSinceCommit >= _rocks.Options.FreezerCommitCadenceBlocks || _commitTimer.Elapsed >= CommitBackstopInterval)
                    CommitOpenBatch();

                _freezeStatsBlocks += count;
                LogFreezeStatsIfDue();
                return (startIndex, count);
            }
            catch
            {
                _openBatch.Reset();
                _openBatch = null;
                _blocksSinceCommit = 0;
                throw;
            }
        }

        private void CommitOpenBatch()
        {
            if (_openBatch == null) return;
            var swCommit = System.Diagnostics.Stopwatch.StartNew();
            _openBatch.Commit();
            _freezeCommitMs += swCommit.Elapsed.TotalMilliseconds;
            _openBatch = null;
            _blocksSinceCommit = 0;
            _commitTimer.Restart();
        }

        internal void CommitPending()
        {
            lock (_freezerAppendLock) CommitOpenBatch();
        }

        internal T CommitPendingThen<T>(Func<T> op)
        {
            lock (_freezerAppendLock)
            {
                CommitOpenBatch();
                return op();
            }
        }

        private void LogFreezeStatsIfDue()
        {
            if (_freezeStatsLog.Elapsed < FreezeStatsLogInterval || _freezeStatsBlocks == 0) return;
            var n = _freezeStatsBlocks;
            Console.Error.WriteLine(
                $"snap.freeze.stats blocks={n} build_us/blk={_freezeBuildMs * 1000 / n:F0} " +
                $"append_us/blk={_freezeAppendMs * 1000 / n:F0} commit_us/blk={_freezeCommitMs * 1000 / n:F0}");
            _freezeStatsBlocks = 0;
            _freezeBuildMs = _freezeAppendMs = _freezeCommitMs = 0;
            _freezeStatsLog.Restart();
        }

        private Nethereum.Freezer.FrozenBlockCluster BuildFrozenCluster(PersistableBlock b)
        {
            var body = new Nethereum.CoreChain.Freezer.BlockBodyCluster(
                txs: b.Transactions,
                uncles: b.Uncles?.ToList(),
                withdrawals: b.Withdrawals?.ToList());

            return new Nethereum.Freezer.FrozenBlockCluster(
                header: _freezerCodecs.Headers.Encode(b.Header),
                hash: _freezerCodecs.Hashes.Encode(b.Hash),
                body: _freezerCodecs.Bodies.Encode(body),
                receipts: _freezerCodecs.Receipts.Encode(ToStoredReceipts(b.Receipts)),
                bal: _freezerCodecs.Bals.Encode(Array.Empty<byte>()));
        }

        private static List<Nethereum.CoreChain.Freezer.ReceiptForStorage> ToStoredReceipts(IReadOnlyList<ReceiptSaveItem> receipts)
        {
            var result = new List<Nethereum.CoreChain.Freezer.ReceiptForStorage>(receipts?.Count ?? 0);
            if (receipts == null) return result;
            foreach (var r in receipts)
                result.Add(new Nethereum.CoreChain.Freezer.ReceiptForStorage(
                    r.Receipt.PostStateOrStatus, r.Receipt.CumulativeGasUsed.ToBigInteger(), r.Receipt.Logs));
            return result;
        }

        private static void RequireBalActivationCompatible(Nethereum.Model.BlockHeader header)
        {
            if (header.BlockAccessListHash == null) return;
            if (ByteUtil.AreEqual(header.BlockAccessListHash, EmptyBlockAccessListHash)) return;

            throw new InvalidOperationException(
                $"cannot freeze block {header.BlockNumber.ToBigInteger()} at/above BAL activation without its " +
                "BAL: PersistableBlock carries no BlockAccessList, so freezing would durably write the empty " +
                "placeholder for a block whose real access list is not empty.");
        }
    }
}
