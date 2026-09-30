using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Nethereum.Documentation;
using Nethereum.Freezer;
using Nethereum.Freezer.FilterMaps;
using Nethereum.Model;

namespace Nethereum.CoreChain.Freezer.FilterMaps
{
    public sealed class FilterMapsIndexer
    {
        private const uint SchemaVersion = 2;

        private readonly IFilterMapsStore _store;
        private readonly IChainView _chain;
        private readonly IFinalitySource _finality;
        private readonly FilterMapsParams _p;
        private readonly int _renderDegreeOfParallelism;

        private PendingEpoch _pending;
        private long _lvValueLowerBound;
        private long _lvCountedThroughBlock;
        private long _emitMilliseconds;
        private long _passAMilliseconds;
        private long _renderMilliseconds;
        private long _writeMilliseconds;
        private int _maxParallelMapBatchSize;
        private long _valuesHashedByParallelEmit;

        public FilterMapsIndexer(
            IFilterMapsStore store, IChainView chain, IFinalitySource finality, FilterMapsParams p,
            int renderDegreeOfParallelism = 1)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _chain = chain ?? throw new ArgumentNullException(nameof(chain));
            _finality = finality ?? throw new ArgumentNullException(nameof(finality));
            _p = p;
            _renderDegreeOfParallelism = renderDegreeOfParallelism;
        }

        public FilterMapsParams Params => _p;

        public int RenderDegreeOfParallelism => _renderDegreeOfParallelism;

        public long LogValueLowerBound => _lvValueLowerBound;

        public long LogValueCountedThroughBlock => _lvCountedThroughBlock;

        public long LastEmitMilliseconds => _emitMilliseconds;

        public long LastPassAMilliseconds => _passAMilliseconds;

        public long LastRenderMilliseconds => _renderMilliseconds;

        public long LastWriteMilliseconds => _writeMilliseconds;

        public int MaxParallelMapBatchSize => _maxParallelMapBatchSize;

        public long ValuesHashedByParallelEmit => _valuesHashedByParallelEmit;

        public long ResumeCursor(long committedBlocksAfterLast) =>
            _pending != null && _pending.EpochStartBlock == committedBlocksAfterLast
                ? _pending.NextBlockToWalk
                : committedBlocksAfterLast;

        public long IndexedHeadBlock
        {
            get
            {
                var range = _store.ReadRange();
                return range == null ? -1 : range.BlocksAfterLast - 1;
            }
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "FilterMapsIndexer.RenderHead — render one finalized epoch into the fm- index")]
        public bool RenderHead()
        {
            var range = _store.ReadRange() ?? EmptyRange();
            var fromBlock = range.BlocksAfterLast;
            var finalizedBlock = _finality.FinalizedBlockNumber;
            if (fromBlock > finalizedBlock)
                return false;

            _emitMilliseconds = 0;
            _passAMilliseconds = 0;
            _renderMilliseconds = 0;
            _writeMilliseconds = 0;

            var epoch = RenderOneEpoch(range, fromBlock, finalizedBlock, _chain);
            if (epoch == null)
                return false;

            CommitTimed(range, epoch);
            return true;
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "FilterMapsIndexer.RenderChunk — render every complete epoch spanned by one already-decoded receipts chunk")]
        public int RenderChunk(IReadOnlyList<(long BlockNumber, IReadOnlyList<ReceiptForStorage> Receipts)> chunk, long finalizedBlockBound)
        {
            if (chunk == null || chunk.Count == 0) return 0;

            var chunkView = new ChunkChainView(chunk);
            var chunkLastBlock = chunk[chunk.Count - 1].BlockNumber;
            var toBlock = Math.Min(finalizedBlockBound, chunkLastBlock);
            var epochs = 0;

            _emitMilliseconds = 0;
            _passAMilliseconds = 0;
            _renderMilliseconds = 0;
            _writeMilliseconds = 0;

            while (true)
            {
                var range = _store.ReadRange() ?? EmptyRange();
                var fromBlock = range.BlocksAfterLast;
                if (fromBlock > toBlock)
                    break;

                var epoch = RenderOneEpoch(range, fromBlock, toBlock, chunkView);
                if (epoch == null)
                    break;

                CommitTimed(range, epoch);
                epochs++;
            }

            return epochs;
        }

        private sealed class ChunkChainView : IChainView
        {
            private readonly IReadOnlyList<(long BlockNumber, IReadOnlyList<ReceiptForStorage> Receipts)> _chunk;
            private readonly long _baseBlock;

            public ChunkChainView(IReadOnlyList<(long BlockNumber, IReadOnlyList<ReceiptForStorage> Receipts)> chunk)
            {
                _chunk = chunk;
                _baseBlock = chunk[0].BlockNumber;
            }

            public long HeadNumber => throw new NotSupportedException("ChunkChainView only serves Receipts() for the chunk it wraps");

            public byte[] BlockId(long number) => throw new NotSupportedException("ChunkChainView only serves Receipts() for the chunk it wraps");

            public BlockHeader Header(long number) => throw new NotSupportedException("ChunkChainView only serves Receipts() for the chunk it wraps");

            public IReadOnlyList<ReceiptForStorage> Receipts(long number)
            {
                var index = number - _baseBlock;
                if (index < 0 || index >= _chunk.Count)
                    throw new ArgumentOutOfRangeException(nameof(number), number, "block is outside the wrapped chunk");
                return _chunk[(int)index].Receipts;
            }
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "FilterMapsIndexer.RollbackTo — un-render on finality regression")]
        public bool RollbackTo(long newHead)
        {
            var range = _store.ReadRange();
            if (range == null || range.BlocksAfterLast - 1 <= newHead)
                return false;

            var newMapsAfterLast = LastWholeEpochBoundaryAtOrBelow(range, newHead);
            if (newMapsAfterLast >= range.MapsAfterLast)
                return false;

            DeleteMapRows(newMapsAfterLast, range.MapsAfterLast);
            _pending = null;

            long newBlocksAfterLast;
            long newHeadDelimiter;
            if (newMapsAfterLast <= range.MapsFirst)
            {
                newBlocksAfterLast = range.BlocksFirst;
                newHeadDelimiter = 0;
            }
            else
            {
                var lastKeptMap = _store.ReadLastBlockOfMap(newMapsAfterLast - 1);
                if (!lastKeptMap.HasValue)
                    throw new FreezerConsistencyException(
                        $"filter maps rollback: missing last-block-of-map for map {newMapsAfterLast - 1}");

                newBlocksAfterLast = lastKeptMap.Value.blockNumber + 1;
                var nextBlockStart = _store.ReadBlockLvPointer(newBlocksAfterLast);
                newHeadDelimiter = nextBlockStart.HasValue ? nextBlockStart.Value - 1 : 0;
            }

            _store.WriteRange(new FilterMapsRange(
                range.Version,
                headIndexed: true,
                headDelimiter: newHeadDelimiter,
                blocksFirst: range.BlocksFirst,
                blocksAfterLast: newBlocksAfterLast,
                mapsFirst: range.MapsFirst,
                mapsAfterLast: newMapsAfterLast,
                tailPartialEpoch: 0));
            return true;
        }


        private sealed class EpochRenderResult
        {
            public FilterMapRenderer Renderer;
            public Dictionary<long, long> LastBlockOfMap;
            public Dictionary<long, long> BlockLvPointers;
            public long NewMapsAfterLast;
            public long NewBlocksAfterLast;
            public long NewHeadDelimiter;
        }

        private sealed class PendingEpoch
        {
            public long EpochStartBlock;
            public long TargetMapsAfterLast;
            public long TargetLvIndex;
            public FilterMapRenderer Renderer;
            public Dictionary<long, long> LastBlockOfMap;
            public Dictionary<long, long> BlockLvPointers;
            public long LastProcessedBlock;
            public long HeadDelimiter;
            public long NextBlockToWalk;
            public long? OpenMapIndex;
            public List<(long LvIndex, byte[] ValueHash)> OpenMapEntries;
        }

        private PendingEpoch StartNewPending(FilterMapsRange range, long fromBlock)
        {
            var targetMapsAfterLast = range.MapsAfterLast + _p.MapsPerEpoch;
            var pending = new PendingEpoch
            {
                EpochStartBlock = fromBlock,
                TargetMapsAfterLast = targetMapsAfterLast,
                TargetLvIndex = targetMapsAfterLast * (long)_p.ValuesPerMap,
                Renderer = new FilterMapRenderer(_p),
                LastBlockOfMap = new Dictionary<long, long>(),
                BlockLvPointers = new Dictionary<long, long>(),
                LastProcessedBlock = fromBlock - 1,
                HeadDelimiter = range.HeadDelimiter,
                NextBlockToWalk = fromBlock,
                OpenMapIndex = null,
                OpenMapEntries = new List<(long LvIndex, byte[] ValueHash)>(),
            };
            if (fromBlock == 0)
                pending.BlockLvPointers[fromBlock] = range.HeadDelimiter;
            return pending;
        }

        private EpochRenderResult RenderOneEpoch(FilterMapsRange range, long fromBlock, long finalizedBlock, IChainView chain)
        {
            var pending = _pending != null && _pending.EpochStartBlock == fromBlock
                ? _pending
                : StartNewPending(range, fromBlock);

            if (pending.NextBlockToWalk > finalizedBlock)
            {
                _pending = pending;
                return null;
            }

            var walkFromBlock = pending.NextBlockToWalk;
            long? precedingBlock = walkFromBlock > 0 ? walkFromBlock - 1 : (long?)null;
            var completed = false;
            var readyMaps = new Dictionary<long, List<(long LvIndex, byte[] ValueHash)>>();
            var deferValueHashing = _renderDegreeOfParallelism > 1;

            var passASw = Stopwatch.StartNew();
            foreach (var entry in LogValueSequence.Enumerate(walkFromBlock, finalizedBlock, pending.HeadDelimiter, precedingBlock, chain, _p, deferValueHashing))
            {
                var mapIndex = entry.LvIndex >> _p.LogValuesPerMap;
                pending.LastBlockOfMap[mapIndex] = entry.BlockNumber;

                if (pending.OpenMapIndex.HasValue && pending.OpenMapIndex.Value != mapIndex)
                {
                    if (pending.OpenMapEntries.Count > 0)
                        readyMaps[pending.OpenMapIndex.Value] = pending.OpenMapEntries;
                    pending.OpenMapEntries = new List<(long LvIndex, byte[] ValueHash)>();
                }
                pending.OpenMapIndex = mapIndex;

                if (entry.IsBlockDelimiter)
                {
                    pending.BlockLvPointers[entry.BlockNumber + 1] = entry.LvIndex + 1;
                    pending.LastProcessedBlock = entry.BlockNumber;
                    if (entry.BlockNumber + 1 > _lvCountedThroughBlock)
                        _lvCountedThroughBlock = entry.BlockNumber + 1;

                    if (entry.LvIndex >= pending.TargetLvIndex)
                    {
                        pending.HeadDelimiter = entry.LvIndex;
                        completed = true;
                        break;
                    }

                    pending.HeadDelimiter = entry.LvIndex + 1;
                }
                else
                {
                    pending.OpenMapEntries.Add((entry.LvIndex, entry.ValueHash));
                    pending.HeadDelimiter = entry.LvIndex + 1;
                    _lvValueLowerBound++;
                    if (entry.BlockNumber + 1 > _lvCountedThroughBlock)
                        _lvCountedThroughBlock = entry.BlockNumber + 1;
                }
            }
            passASw.Stop();
            _passAMilliseconds += passASw.ElapsedMilliseconds;

            if (!completed && pending.HeadDelimiter >= pending.TargetLvIndex)
            {
                pending.LastProcessedBlock = finalizedBlock;
                completed = true;
            }

            if (completed && pending.OpenMapIndex.HasValue && pending.OpenMapEntries.Count > 0)
            {
                readyMaps[pending.OpenMapIndex.Value] = pending.OpenMapEntries;
                pending.OpenMapEntries = new List<(long LvIndex, byte[] ValueHash)>();
            }

            if (deferValueHashing)
            {
                var emitSw = Stopwatch.StartNew();
                _valuesHashedByParallelEmit += LogValueHashEmitter.HashCollectedValues(readyMaps, _renderDegreeOfParallelism);
                emitSw.Stop();
                _emitMilliseconds += emitSw.ElapsedMilliseconds;
            }

            var renderSw = Stopwatch.StartNew();
            RenderReadyMaps(pending.Renderer, readyMaps);
            renderSw.Stop();
            _renderMilliseconds += renderSw.ElapsedMilliseconds;

            if (!completed)
            {
                pending.NextBlockToWalk = finalizedBlock + 1;
                _pending = pending;
                return null;
            }

            _pending = null;

            var newMapsAfterLast = Math.Min(pending.HeadDelimiter / _p.ValuesPerMap, pending.TargetMapsAfterLast);
            var committedBoundaryLv = newMapsAfterLast * (long)_p.ValuesPerMap;

            var newBlocksAfterLast = pending.LastProcessedBlock + 1;
            var newHeadDelimiter = pending.HeadDelimiter;
            if (pending.HeadDelimiter != committedBoundaryLv)
            {
                (newBlocksAfterLast, newHeadDelimiter) = LastFullyCommittedBlock(
                    pending.BlockLvPointers, fromBlock, pending.LastProcessedBlock, committedBoundaryLv,
                    fallbackBlocksAfterLast: range.BlocksAfterLast, fallbackHeadDelimiter: range.HeadDelimiter);
            }

            return new EpochRenderResult
            {
                Renderer = pending.Renderer,
                LastBlockOfMap = pending.LastBlockOfMap,
                BlockLvPointers = pending.BlockLvPointers,
                NewMapsAfterLast = newMapsAfterLast,
                NewBlocksAfterLast = newBlocksAfterLast,
                NewHeadDelimiter = newHeadDelimiter,
            };
        }

        private static (long blocksAfterLast, long headDelimiter) LastFullyCommittedBlock(
            Dictionary<long, long> blockLvPointers, long fromBlock, long lastProcessedBlock,
            long committedBoundaryLv, long fallbackBlocksAfterLast, long fallbackHeadDelimiter)
        {
            var blocksAfterLast = fallbackBlocksAfterLast;
            var headDelimiter = fallbackHeadDelimiter;

            for (var block = fromBlock + 1; block <= lastProcessedBlock + 1; block++)
            {
                if (!blockLvPointers.TryGetValue(block, out var contentStart))
                    break;

                var delimiterLv = contentStart - 1;
                if (delimiterLv > committedBoundaryLv)
                    break;

                blocksAfterLast = block;
                headDelimiter = delimiterLv;
            }

            return (blocksAfterLast, headDelimiter);
        }

        private void RenderReadyMaps(FilterMapRenderer renderer, Dictionary<long, List<(long LvIndex, byte[] ValueHash)>> readyMaps)
        {
            if (readyMaps.Count == 0)
                return;

            if (_renderDegreeOfParallelism <= 1)
            {
                foreach (var mapEntries in readyMaps)
                    foreach (var value in mapEntries.Value)
                        renderer.Mark(mapEntries.Key, value.LvIndex, value.ValueHash);
                return;
            }

            var mapIndices = new long[readyMaps.Count];
            readyMaps.Keys.CopyTo(mapIndices, 0);
            _maxParallelMapBatchSize = Math.Max(_maxParallelMapBatchSize, mapIndices.Length);
            var perMapRenderers = new FilterMapRenderer[mapIndices.Length];
            var options = new ParallelOptions { MaxDegreeOfParallelism = FrozenParallelism.Resolve(_renderDegreeOfParallelism) };

            Parallel.For(0, mapIndices.Length, options, i =>
            {
                var mapIndex = mapIndices[i];
                var mapRenderer = new FilterMapRenderer(_p);
                foreach (var value in readyMaps[mapIndex])
                    mapRenderer.Mark(mapIndex, value.LvIndex, value.ValueHash);
                perMapRenderers[i] = mapRenderer;
            });

            for (var i = 0; i < mapIndices.Length; i++)
                renderer.AdoptMap(mapIndices[i], perMapRenderers[i]);
        }

        private void CommitTimed(FilterMapsRange range, EpochRenderResult epoch)
        {
            var writeSw = Stopwatch.StartNew();
            Commit(range, epoch);
            writeSw.Stop();
            _writeMilliseconds += writeSw.ElapsedMilliseconds;
        }

        private void Commit(FilterMapsRange range, EpochRenderResult epoch)
        {
            WriteMapRows(epoch.Renderer, range.MapsAfterLast, epoch.NewMapsAfterLast);

            foreach (var kv in epoch.LastBlockOfMap)
            {
                if (kv.Key < range.MapsAfterLast || kv.Key >= epoch.NewMapsAfterLast)
                    continue;
                _store.WriteLastBlockOfMap(kv.Key, kv.Value, _chain.BlockId(kv.Value));
            }

            foreach (var kv in epoch.BlockLvPointers)
            {
                if (kv.Key >= epoch.NewBlocksAfterLast)
                    continue;
                _store.WriteBlockLvPointer(kv.Key, kv.Value);
            }

            _store.WriteRange(new FilterMapsRange(
                SchemaVersion,
                headIndexed: true,
                headDelimiter: epoch.NewHeadDelimiter,
                blocksFirst: range.BlocksFirst,
                blocksAfterLast: epoch.NewBlocksAfterLast,
                mapsFirst: range.MapsFirst,
                mapsAfterLast: epoch.NewMapsAfterLast,
                tailPartialEpoch: range.TailPartialEpoch));
        }

        private void WriteMapRows(FilterMapRenderer renderer, long fromMapInclusive, long toMapExclusive)
        {
            var groupSize = _p.BaseRowGroupSize;
            var groupRows = new Dictionary<(long groupStart, int rowIndex), FilterRow[]>();

            foreach (var mapIndex in renderer.TouchedMapIndices)
            {
                if (mapIndex < fromMapInclusive || mapIndex >= toMapExclusive)
                    continue;

                var groupStart = mapIndex - (mapIndex % groupSize);
                var groupLength = (int)Math.Min(groupSize, toMapExclusive - groupStart);
                var offsetInGroup = (int)(mapIndex - groupStart);

                foreach (var kv in renderer.RowsOfMap(mapIndex))
                {
                    var rowIndex = kv.Key;
                    var columns = kv.Value;

                    List<uint> baseColumns;
                    byte[] extEncoded;
                    if (columns.Count > _p.BaseRowLength)
                    {
                        baseColumns = columns.GetRange(0, _p.BaseRowLength);
                        var extColumns = columns.GetRange(_p.BaseRowLength, columns.Count - _p.BaseRowLength);
                        extEncoded = FilterMapsRowCodec.EncodeExtRow(new FilterRow(extColumns), _p);
                    }
                    else
                    {
                        baseColumns = columns;
                        extEncoded = null;
                    }

                    _store.WriteExtRow(FilterMapsSchema.MapRowIndex(mapIndex, rowIndex, _p), extEncoded);

                    var key = (groupStart, rowIndex);
                    if (!groupRows.TryGetValue(key, out var rowsOfGroup))
                    {
                        rowsOfGroup = NewEmptyGroup(groupLength);
                        groupRows[key] = rowsOfGroup;
                    }

                    rowsOfGroup[offsetInGroup] = new FilterRow(baseColumns);
                }
            }

            foreach (var kv in groupRows)
            {
                var groupMapRowIndex = FilterMapsSchema.MapRowIndex(kv.Key.groupStart, kv.Key.rowIndex, _p);
                _store.WriteBaseRowGroup(groupMapRowIndex, FilterMapsRowCodec.EncodeBaseRowGroup(kv.Value, _p));
            }
        }

        private static FilterRow[] NewEmptyGroup(int length)
        {
            var rows = new FilterRow[length];
            for (var i = 0; i < length; i++)
                rows[i] = FilterRow.Empty;
            return rows;
        }


        private long LastWholeEpochBoundaryAtOrBelow(FilterMapsRange range, long newHead)
        {
            var epoch = range.MapsAfterLast;
            while (epoch > range.MapsFirst)
            {
                var lastBlockOfPriorMap = _store.ReadLastBlockOfMap(epoch - 1);
                if (lastBlockOfPriorMap.HasValue && lastBlockOfPriorMap.Value.blockNumber <= newHead)
                    return epoch;
                epoch -= _p.MapsPerEpoch;
            }
            return range.MapsFirst;
        }

        private void DeleteMapRows(long fromMapInclusive, long toMapExclusive)
        {
            var groupSize = _p.BaseRowGroupSize;
            var groupStart = fromMapInclusive - (fromMapInclusive % groupSize);

            for (var rowIndex = 0; rowIndex < _p.MapHeight; rowIndex++)
            {
                for (var mapIndex = fromMapInclusive; mapIndex < toMapExclusive; mapIndex++)
                    _store.WriteExtRow(FilterMapsSchema.MapRowIndex(mapIndex, rowIndex, _p), null);

                for (var g = groupStart; g < toMapExclusive; g += groupSize)
                    _store.WriteBaseRowGroup(FilterMapsSchema.MapRowIndex(g, rowIndex, _p), null);
            }
        }


        private FilterMapsRange EmptyRange() =>
            new FilterMapsRange(SchemaVersion, headIndexed: false, headDelimiter: 0,
                blocksFirst: 0, blocksAfterLast: 0, mapsFirst: 0, mapsAfterLast: 0, tailPartialEpoch: 0);
    }
}
