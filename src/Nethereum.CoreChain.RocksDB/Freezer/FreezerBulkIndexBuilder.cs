using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.CoreChain.Freezer.FilterMaps;
using Nethereum.Freezer;

namespace Nethereum.CoreChain.RocksDB.Freezer
{
    public sealed class FreezerBulkIndexBuilder
    {
        public const int ByHashChunkTargetBytes = 250 * 1024 * 1024;
        public const int FilterMapsChunkTargetBytes = 250 * 1024 * 1024;
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
        private static readonly string[] FreezerIndexableTableNames = { "hashes", "bodies", "receipts" };

        private readonly Nethereum.Freezer.IFrozenReadSource _freezer;
        private readonly FreezerCodecSet _codecs;
        private readonly History.BulkIndexIngestor _ingestor;
        private readonly Stores.RocksDbFilterMapsStore _fmStore;
        private readonly Nethereum.CoreChain.Freezer.FilterMaps.FilterMapsIndexer _fmIndexer;
        private readonly IFreezerIndexProgress _progress;
        private readonly int _byHashDegreeOfParallelism;
        private readonly int _logDegreeOfParallelism;
        private readonly Func<bool> _shouldPause;
        private readonly object _filterMapsRenderLock = new object();

        public FreezerBulkIndexBuilder(
            Nethereum.Freezer.IFrozenReadSource freezer,
            FreezerCodecSet codecs,
            History.BulkIndexIngestor ingestor,
            Stores.RocksDbFilterMapsStore fmStore,
            Nethereum.CoreChain.Freezer.FilterMaps.FilterMapsIndexer fmIndexer,
            IFreezerIndexProgress progress,
            int byHashDegreeOfParallelism = 0,
            int logDegreeOfParallelism = 0,
            Func<bool> shouldPause = null)
        {
            _freezer = freezer;
            _codecs = codecs;
            _ingestor = ingestor;
            _fmStore = fmStore;
            _fmIndexer = fmIndexer;
            _progress = progress;
            _byHashDegreeOfParallelism = byHashDegreeOfParallelism;
            _logDegreeOfParallelism = logDegreeOfParallelism;
            _shouldPause = shouldPause ?? (() => false);
        }

        public long ComputeIndexableBoundary() => _freezer.CommittedHead(FreezerIndexableTableNames);

        public long ByHashIndexedHead => _freezer == null ? 0 : (long)_progress.GetByHashCursor();

        public long LogIndexRenderedHead => _fmStore?.ReadRange()?.BlocksAfterLast ?? 0;

        public long LogRenderProgressBlock => _fmIndexer?.LogValueCountedThroughBlock ?? 0;

        public static void AppendByHashIndexEntries(
            List<(string Cf, byte[] Key, byte[] Value)> entries, ulong number, byte[] blockHash, IReadOnlyList<byte[]> txHashes)
        {
            var blockKey = Storage.History.HistoryKeys.BlockKey(number);
            entries.Add((History.HistoryColumnFamilies.BlockHashIndex, blockHash, blockKey));

            if (txHashes == null) return;
            for (var txIndex = 0; txIndex < txHashes.Count; txIndex++)
            {
                var txHash = txHashes[txIndex];
                if (txHash == null) continue;
                entries.Add((History.HistoryColumnFamilies.TxHashIndex, txHash, Storage.History.HistoryKeys.TxKey(number, (uint)txIndex)));
            }
        }

        public void CatchUpByHash(long head, CancellationToken ct)
        {
            var cursor = (long)_progress.GetByHashCursor();
            if (cursor >= head) return;

            while (cursor < head)
            {
                if (ct.IsCancellationRequested) return;

                WaitForCompactionToDrain(ct);
                if (ct.IsCancellationRequested) return;

                var bodiesChunk = _freezer.DecodeSealedChunk(
                    "bodies", cursor, ByHashChunkTargetBytes, head,
                    maxDegreeOfParallelism: _byHashDegreeOfParallelism);
                var count = bodiesChunk.Count;
                if (count == 0)
                    throw new Nethereum.Freezer.FreezerConsistencyException(
                        $"by-hash chunk made no progress at {cursor} (head {head}); freezer 'bodies' table produced zero items");

                var hashesChunk = _freezer.DecodeSealedRange("hashes", cursor, count, head, maxDegreeOfParallelism: 1);
                var projected = Nethereum.CoreChain.Freezer.FrozenBlockHashProjector.ProjectChunk(
                    bodiesChunk, hashesChunk, _codecs,
                    maxDegreeOfParallelism: _byHashDegreeOfParallelism);

                var entries = new List<(string Cf, byte[] Key, byte[] Value)>();
                ulong windowHead = 0;
                for (var i = 0; i < projected.Count; i++)
                {
                    var number = (ulong)(cursor + i);
                    AppendByHashIndexEntries(entries, number, projected[i].BlockHash, projected[i].TxHashes);
                    windowHead = number;
                }

                if (_ingestor.Accumulate(projected.Count, windowHead, entries))
                {
                    if (ct.IsCancellationRequested) return;
                    _ingestor.Checkpoint();
                }

                cursor += projected.Count;
            }

            if (ct.IsCancellationRequested) return;
            _ingestor.Checkpoint();
        }

        private void WaitForCompactionToDrain(CancellationToken ct)
        {
            while (_shouldPause() && !ct.IsCancellationRequested)
                ct.WaitHandle.WaitOne(PollInterval);
        }

        public int RenderFilterMaps(CancellationToken ct = default, long? headOverride = null)
        {
            if (_fmIndexer == null) return 0;

            var stepLog = FreezerParallelSegment.StepLog;

            lock (_filterMapsRenderLock)
            {
                var head = headOverride ?? _freezer.Items;
                var committedBlocksAfterLast = _fmStore.ReadRange()?.BlocksAfterLast ?? 0;
                var cursor = _fmIndexer.ResumeCursor(committedBlocksAfterLast);

                _fmStore.BeginBulk();
                try
                {
                    var epochs = 0;
                    while (cursor < head && !ct.IsCancellationRequested)
                    {
                        WaitForCompactionToDrain(ct);
                        if (ct.IsCancellationRequested) break;

                        var rawChunk = _freezer.DecodeSealedChunk(
                            "receipts", cursor, FilterMapsChunkTargetBytes, head,
                            maxDegreeOfParallelism: _logDegreeOfParallelism);
                        if (rawChunk.Count == 0) break;

                        var decodeSw = stepLog == null ? null : Stopwatch.StartNew();
                        var decodedChunk = DecodeReceiptsChunk(rawChunk);
                        decodeSw?.Stop();

                        epochs += _fmIndexer.RenderChunk(decodedChunk, head - 1);
                        cursor += decodedChunk.Length;

                        stepLog?.Invoke(
                            $"filtermaps chunk items={decodedChunk.Length} decode_ms={decodeSw.ElapsedMilliseconds} " +
                            $"emit_ms={_fmIndexer.LastEmitMilliseconds} passA_ms={_fmIndexer.LastPassAMilliseconds} " +
                            $"render_ms={_fmIndexer.LastRenderMilliseconds} write_ms={_fmIndexer.LastWriteMilliseconds} " +
                            $"dop={_fmIndexer.RenderDegreeOfParallelism}");
                    }
                    var endBulkSw = stepLog == null ? null : Stopwatch.StartNew();
                    _fmStore.EndBulk();
                    endBulkSw?.Stop();
                    if (endBulkSw != null)
                        stepLog($"filtermaps store flush end_bulk_ms={endBulkSw.ElapsedMilliseconds}");
                    _progress.SetFilterMapsLvProgress(_fmIndexer.LogValueLowerBound, _fmIndexer.LogValueCountedThroughBlock);
                    return epochs;
                }
                catch
                {
                    try { _fmStore.EndBulk(); } catch { }
                    throw;
                }
            }
        }

        private (long BlockNumber, IReadOnlyList<Nethereum.CoreChain.Freezer.ReceiptForStorage> Receipts)[] DecodeReceiptsChunk(
            IReadOnlyList<(long BlockNumber, byte[] Decoded)> rawChunk)
        {
            var result = new (long BlockNumber, IReadOnlyList<Nethereum.CoreChain.Freezer.ReceiptForStorage> Receipts)[rawChunk.Count];
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Nethereum.Freezer.FrozenParallelism.Resolve(_logDegreeOfParallelism)
            };
            Parallel.For(0, rawChunk.Count, options, i =>
                result[i] = (rawChunk[i].BlockNumber, _codecs.Receipts.Decode(rawChunk[i].Decoded)));
            return result;
        }

        public (ulong Previous, ulong New) AlignCursorToHead()
        {
            var previous = _progress.GetByHashCursor();
            if (_freezer == null) return (previous, previous);
            var head = (ulong)_freezer.Items;
            if (head <= previous) return (previous, previous);
            _progress.SetByHashCursor(head);
            return (previous, head);
        }

        public void SetByHashCursor(ulong itemNumber) => _progress.SetByHashCursor(itemNumber);
    }
}
