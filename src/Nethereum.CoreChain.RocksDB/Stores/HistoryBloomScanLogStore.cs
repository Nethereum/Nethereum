using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Serialization;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.History;
using Nethereum.Model;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public class HistoryBloomScanLogStore : ILogStore
    {
        private readonly RocksDbManager _manager;
        private readonly RocksDbSerializer _serializer;
        private readonly RocksDbChainMetadataStore _promotionCursor;

        public HistoryBloomScanLogStore(RocksDbManager manager, RocksDbSerializer serializer = null,
            RocksDbChainMetadataStore promotionCursor = null)
        {
            _manager = manager;
            _serializer = serializer ?? RocksDbSerializer.Default;
            _promotionCursor = promotionCursor;
        }

        public Task<List<FilteredLog>> GetLogsAsync(LogFilter filter)
        {
            var result = new List<FilteredLog>();
            if (filter == null) return Task.FromResult(result);

            var fromBlock = filter.FromBlock ?? BigInteger.Zero;
            if (fromBlock < BigInteger.Zero) fromBlock = BigInteger.Zero;

            using var snapshot = _manager.CreateSnapshot();
            var readOptions = new ReadOptions().SetSnapshot(snapshot);
            try
            {
                BigInteger toBlock;
                if (filter.ToBlock.HasValue)
                {
                    toBlock = filter.ToBlock.Value;
                }
                else
                {
                    var tip = ResolveTipBlockNumber(readOptions);
                    if (!tip.HasValue) return Task.FromResult(result);
                    toBlock = tip.Value;
                }

                if (toBlock < fromBlock) return Task.FromResult(result);

                var queryTerms = RocksDbLogStore.BuildQueryBloomTerms(filter);
                var cursor = HotCursorOrTop(toBlock, readOptions);

                var historyTo = BigInteger.Min(toBlock, cursor);
                if (fromBlock <= historyTo)
                    ScanBlockRange(HistoryColumnFamilies.BlockMeta, HistoryColumnFamilies.ReceiptBody,
                        fromBlock, historyTo, queryTerms, filter, result, readOptions);

                var hotFrom = BigInteger.Max(fromBlock, cursor + 1);
                if (_promotionCursor != null && hotFrom <= toBlock)
                    ScanBlockRange(RocksDbManager.CF_HOT_BLOCK_META, RocksDbManager.CF_HOT_RECEIPT_BODY,
                        hotFrom, toBlock, queryTerms, filter, result, readOptions);

                return Task.FromResult(result);
            }
            finally
            {
                GC.KeepAlive(readOptions);
            }
        }

        public Task<List<FilteredLog>> GetLogsByTxHashAsync(byte[] txHash)
        {
            var result = new List<FilteredLog>();
            if (txHash == null) return Task.FromResult(result);

            using var snapshot = _manager.CreateSnapshot();
            var readOptions = new ReadOptions().SetSnapshot(snapshot);
            try
            {
                var info = _promotionCursor != null
                    ? ReadReceiptInfoByTxHash(RocksDbManager.CF_HOT_TX_HASH_INDEX, RocksDbManager.CF_HOT_RECEIPT_BODY, txHash, readOptions)
                    : null;
                if (info == null)
                    info = ReadReceiptInfoByTxHash(HistoryColumnFamilies.TxHashIndex, HistoryColumnFamilies.ReceiptBody, txHash, readOptions);

                AppendReceiptLogs(info, result);

                result.Sort((a, b) => a.LogIndex.CompareTo(b.LogIndex));
                return Task.FromResult(result);
            }
            finally
            {
                GC.KeepAlive(readOptions);
            }
        }

        public Task<List<FilteredLog>> GetLogsByBlockHashAsync(byte[] blockHash)
        {
            var result = new List<FilteredLog>();
            if (blockHash == null) return Task.FromResult(result);

            using var snapshot = _manager.CreateSnapshot();
            var readOptions = new ReadOptions().SetSnapshot(snapshot);
            try
            {
                if (_promotionCursor != null)
                {
                    var hotLoc = _manager.Get(RocksDbManager.CF_HOT_BLOCK_HASH_INDEX, blockHash, readOptions);
                    if (hotLoc != null && hotLoc.Length >= HistoryKeys.BlockKeyLength)
                    {
                        result.AddRange(ReadBlockLogs(RocksDbManager.CF_HOT_RECEIPT_BODY, HistoryKeys.ReadBlockNumber(hotLoc), readOptions));
                        SortByTxThenLog(result);
                        return Task.FromResult(result);
                    }
                }

                var numBytes = _manager.Get(HistoryColumnFamilies.BlockHashIndex, blockHash, readOptions);
                if (numBytes == null || numBytes.Length < HistoryKeys.BlockKeyLength) return Task.FromResult(result);

                result.AddRange(ReadBlockLogs(HistoryColumnFamilies.ReceiptBody, HistoryKeys.ReadBlockNumber(numBytes), readOptions));
                SortByTxThenLog(result);
                return Task.FromResult(result);
            }
            finally
            {
                GC.KeepAlive(readOptions);
            }
        }

        public Task<List<FilteredLog>> GetLogsByBlockNumberAsync(BigInteger blockNumber)
        {
            if (blockNumber < BigInteger.Zero) return Task.FromResult(new List<FilteredLog>());

            using var snapshot = _manager.CreateSnapshot();
            var readOptions = new ReadOptions().SetSnapshot(snapshot);
            try
            {
                var cursor = HotCursorOrTop(blockNumber, readOptions);
                var receiptCf = _promotionCursor != null && blockNumber > cursor
                    ? RocksDbManager.CF_HOT_RECEIPT_BODY
                    : HistoryColumnFamilies.ReceiptBody;

                var result = ReadBlockLogs(receiptCf, (ulong)blockNumber, readOptions);
                SortByTxThenLog(result);
                return Task.FromResult(result);
            }
            finally
            {
                GC.KeepAlive(readOptions);
            }
        }

        public Task SaveLogsAsync(List<Log> logs, byte[] txHash, byte[] blockHash, BigInteger blockNumber, int txIndex)
            => Task.CompletedTask;

        public Task SaveManyLogsAsync(
            IReadOnlyList<(List<Log> Logs, byte[] TxHash, int TxIndex)> txLogs, byte[] blockHash, BigInteger blockNumber)
            => Task.CompletedTask;

        public Task SaveBlockBloomAsync(BigInteger blockNumber, byte[] bloom) => Task.CompletedTask;

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber) => Task.CompletedTask;


        private BigInteger HotCursorOrTop(BigInteger top, ReadOptions readOptions)
            => _promotionCursor != null ? (BigInteger)_promotionCursor.GetPromotionCursor(readOptions) : top;

        private void ScanBlockRange(string metaCf, string receiptCf, BigInteger fromBlock, BigInteger toBlock,
            RocksDbLogStore.QueryBloomTerms queryTerms, LogFilter filter, List<FilteredLog> into, ReadOptions readOptions)
        {
            using var metaIt = _manager.CreateIterator(metaCf, readOptions);
            for (metaIt.Seek(HistoryKeys.BlockKey((ulong)fromBlock)); metaIt.Valid(); metaIt.Next())
            {
                var blockNumber = (BigInteger)HistoryKeys.ReadBlockNumber(metaIt.Key());
                if (blockNumber > toBlock) break;

                var meta = BlockMetaCodec.Decode(metaIt.Value());
                if (meta == null) continue;

                if (!RocksDbLogStore.MatchesQueryBloomTerms(queryTerms, meta.Bloom))
                    continue;

                foreach (var log in ReadBlockLogs(receiptCf, (ulong)blockNumber, readOptions))
                {
                    if (filter.MatchesAddress(log.Address) && filter.MatchesTopics(log.Topics))
                        into.Add(log);
                }
            }
        }

        private List<FilteredLog> ReadBlockLogs(string receiptCf, ulong blockNumber, ReadOptions readOptions)
        {
            var result = new List<FilteredLog>();
            using var it = _manager.CreateIterator(receiptCf, readOptions);
            for (it.Seek(HistoryKeys.TxKey(blockNumber, 0)); it.Valid(); it.Next())
            {
                if (HistoryKeys.ReadBlockNumber(it.Key()) != blockNumber) break;
                var info = _serializer.DeserializeReceiptInfoWith(it.Value());
                AppendReceiptLogs(info, result);
            }
            BlockWideLogIndex.Assign(result);
            return result;
        }

        private static void AppendReceiptLogs(ReceiptInfo info, List<FilteredLog> into)
        {
            var logs = info?.Receipt?.Logs;
            if (logs == null || logs.Count == 0) return;
            for (int i = 0; i < logs.Count; i++)
                into.Add(FilteredLog.FromLog(logs[i], info.BlockHash, info.BlockNumber, info.TxHash, info.TransactionIndex, i));
        }

        private ReceiptInfo ReadReceiptInfoByTxHash(string txHashIndexCf, string receiptBodyCf, byte[] txHash, ReadOptions readOptions)
        {
            var loc = _manager.Get(txHashIndexCf, txHash, readOptions);
            if (loc == null || loc.Length < HistoryKeys.TxKeyLength) return null;
            var bytes = _manager.Get(receiptBodyCf, HistoryKeys.TxKey(HistoryKeys.ReadBlockNumber(loc), HistoryKeys.ReadTxIndex(loc)), readOptions);
            return bytes == null ? null : _serializer.DeserializeReceiptInfoWith(bytes);
        }

        private static void SortByTxThenLog(List<FilteredLog> list) => list.Sort((a, b) =>
        {
            var txCmp = a.TransactionIndex.CompareTo(b.TransactionIndex);
            return txCmp != 0 ? txCmp : a.LogIndex.CompareTo(b.LogIndex);
        });

        private BigInteger? ResolveTipBlockNumber(ReadOptions readOptions)
        {
            if (_promotionCursor != null)
            {
                using var hotIt = _manager.CreateIterator(RocksDbManager.CF_HOT_BLOCK_META, readOptions);
                hotIt.SeekToLast();
                if (hotIt.Valid()) return (BigInteger)HistoryKeys.ReadBlockNumber(hotIt.Key());
            }

            using var it = _manager.CreateIterator(HistoryColumnFamilies.BlockMeta, readOptions);
            it.SeekToLast();
            if (!it.Valid()) return null;
            return (BigInteger)HistoryKeys.ReadBlockNumber(it.Key());
        }
    }
}
