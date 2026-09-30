using System;
using System.Collections.Generic;
using Nethereum.CoreChain.Storage.History;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.History
{
    public sealed class RocksDbHistoryStore : IHistoryStore
    {
        private static readonly byte[] MaxBound = { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff };

        private readonly RocksDb _db;
        private readonly IHistoryReorgDecoder _reorg;
        private readonly ColumnFamilyHandle _txBody, _receiptBody, _blockHeader, _blockMeta, _txHashIndex, _blockHashIndex;

        public RocksDbHistoryStore(RocksDb db, IHistoryReorgDecoder reorgDecoder = null)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _reorg = reorgDecoder;
            _txBody = db.GetColumnFamily(HistoryColumnFamilies.TxBody);
            _receiptBody = db.GetColumnFamily(HistoryColumnFamilies.ReceiptBody);
            _blockHeader = db.GetColumnFamily(HistoryColumnFamilies.BlockHeader);
            _blockMeta = db.GetColumnFamily(HistoryColumnFamilies.BlockMeta);
            _txHashIndex = db.GetColumnFamily(HistoryColumnFamilies.TxHashIndex);
            _blockHashIndex = db.GetColumnFamily(HistoryColumnFamilies.BlockHashIndex);
        }

        public void AppendBlock(HistoryBlockWrite block)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            var blockKey = HistoryKeys.BlockKey(block.BlockNumber);

            using var batch = new WriteBatch();
            if (block.Header != null) batch.Put(blockKey, block.Header, _blockHeader);
            if (block.Meta != null) batch.Put(blockKey, block.Meta, _blockMeta);
            if (block.BlockHash != null) batch.Put(block.BlockHash, blockKey, _blockHashIndex);

            var txs = block.Transactions;
            for (int i = 0; i < txs.Count; i++)
            {
                var t = txs[i];
                var txKey = HistoryKeys.TxKey(block.BlockNumber, (uint)i);
                if (t.Tx != null) batch.Put(txKey, t.Tx, _txBody);
                if (t.Receipt != null) batch.Put(txKey, t.Receipt, _receiptBody);
                if (t.TxHash != null) batch.Put(t.TxHash, txKey, _txHashIndex);
            }
            _db.Write(batch);
        }

        public TxLocation? FindTransaction(byte[] txHash)
        {
            var loc = _db.Get(txHash, _txHashIndex);
            if (loc == null || loc.Length < HistoryKeys.TxKeyLength) return null;
            return new TxLocation(HistoryKeys.ReadBlockNumber(loc), HistoryKeys.ReadTxIndex(loc));
        }

        public ulong? FindBlock(byte[] blockHash)
        {
            var num = _db.Get(blockHash, _blockHashIndex);
            if (num == null || num.Length < HistoryKeys.BlockKeyLength) return null;
            return HistoryKeys.ReadBlockNumber(num);
        }

        public byte[] GetHeader(ulong blockNumber) => _db.Get(HistoryKeys.BlockKey(blockNumber), _blockHeader);
        public byte[] GetBlockMeta(ulong blockNumber) => _db.Get(HistoryKeys.BlockKey(blockNumber), _blockMeta);
        public byte[] GetTransaction(ulong blockNumber, uint txIndex) => _db.Get(HistoryKeys.TxKey(blockNumber, txIndex), _txBody);
        public byte[] GetReceipt(ulong blockNumber, uint txIndex) => _db.Get(HistoryKeys.TxKey(blockNumber, txIndex), _receiptBody);

        public IReadOnlyList<byte[]> GetBlockTransactions(ulong blockNumber)
        {
            var result = new List<byte[]>();
            using var it = _db.NewIterator(_txBody);
            for (it.Seek(HistoryKeys.TxKey(blockNumber, 0)); it.Valid(); it.Next())
            {
                if (HistoryKeys.ReadBlockNumber(it.Key()) != blockNumber) break;
                result.Add(it.Value());
            }
            return result;
        }

        public ulong? GetTip()
        {
            using var it = _db.NewIterator(_blockHeader);
            it.SeekToLast();
            if (!it.Valid()) return null;
            var key = it.Key();
            if (key.Length < HistoryKeys.BlockKeyLength) return null;
            return HistoryKeys.ReadBlockNumber(key);
        }

        public void HandleReorg(ulong revertToBlock)
        {
            var from = revertToBlock + 1;
            var blockFrom = HistoryKeys.BlockKey(from);
            var txFrom = HistoryKeys.TxKey(from, 0);

            using var batch = new WriteBatch();

            if (_reorg != null)
            {
                using (var it = _db.NewIterator(_blockMeta))
                    for (it.Seek(blockFrom); it.Valid(); it.Next())
                    {
                        var h = _reorg.BlockHash(it.Value());
                        if (h != null) batch.Delete(h, _blockHashIndex);
                    }
                using (var it = _db.NewIterator(_txBody))
                    for (it.Seek(txFrom); it.Valid(); it.Next())
                    {
                        var h = _reorg.TxHash(it.Value());
                        if (h != null) batch.Delete(h, _txHashIndex);
                    }
            }

            batch.DeleteRange(txFrom, (ulong)txFrom.Length, MaxBound, (ulong)MaxBound.Length, _txBody);
            batch.DeleteRange(txFrom, (ulong)txFrom.Length, MaxBound, (ulong)MaxBound.Length, _receiptBody);
            batch.DeleteRange(blockFrom, (ulong)blockFrom.Length, MaxBound, (ulong)MaxBound.Length, _blockHeader);
            batch.DeleteRange(blockFrom, (ulong)blockFrom.Length, MaxBound, (ulong)MaxBound.Length, _blockMeta);

            _db.Write(batch);
        }
    }
}
