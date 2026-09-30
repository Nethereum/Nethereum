using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.CoreChain.RocksDB.Serialization;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.History;
using Nethereum.Model;
using Nethereum.Util;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class RocksDbHotBlockWindowStore
    {
        private readonly RocksDbManager _core;
        private readonly IBlockEncodingProvider _provider;
        private readonly RocksDbSerializer _serializer;
        private readonly ColumnFamilyHandle _header, _meta, _blockHashIndex, _txBody, _txHashIndex, _receiptBody, _blockAccessList;
        private readonly bool _evictOnWrite;

        public int WindowSize { get; }

        public RocksDbHotBlockWindowStore(RocksDbManager coreManager, int windowSize,
            IBlockEncodingProvider provider = null, bool evictOnWrite = true, RocksDbSerializer serializer = null)
        {
            _core = coreManager ?? throw new ArgumentNullException(nameof(coreManager));
            WindowSize = windowSize > 0 ? windowSize : 128;
            _provider = provider ?? RlpBlockEncodingProvider.Instance;
            _serializer = serializer ?? RocksDbSerializer.Default;
            _evictOnWrite = evictOnWrite;
            _header = coreManager.GetColumnFamily(RocksDbManager.CF_HOT_BLOCK_HEADER);
            _meta = coreManager.GetColumnFamily(RocksDbManager.CF_HOT_BLOCK_META);
            _blockHashIndex = coreManager.GetColumnFamily(RocksDbManager.CF_HOT_BLOCK_HASH_INDEX);
            _txBody = coreManager.GetColumnFamily(RocksDbManager.CF_HOT_TX_BODY);
            _txHashIndex = coreManager.GetColumnFamily(RocksDbManager.CF_HOT_TX_HASH_INDEX);
            _receiptBody = coreManager.GetColumnFamily(RocksDbManager.CF_HOT_RECEIPT_BODY);
            _blockAccessList = coreManager.GetColumnFamily(RocksDbManager.CF_HOT_BLOCK_ACCESS_LIST);
        }

        private ulong Floor(ulong head) => head + 1 >= (ulong)WindowSize ? head + 1 - (ulong)WindowSize : 0;


        public void WriteHeader(BlockHeader header, byte[] blockHash)
        {
            if (header == null || blockHash == null) return;
            var number = (ulong)header.BlockNumber.ToBigInteger();
            using var batch = _core.CreateWriteBatch();
            var key = HistoryKeys.BlockKey(number);
            batch.Put(key, _provider.EncodeBlockHeader(header), _header);
            batch.Put(key, BlockMetaCodec.Encode(new BlockMeta { BlockHash = blockHash, TxCount = 0, Bloom = header.LogsBloom }), _meta);
            batch.Put(blockHash, key, _blockHashIndex);
            if (_evictOnWrite) StageEvictBelow(batch, Floor(number));
            StageTipHeightAdvance(batch, number);
            _core.Write(batch);
        }

        private const string HeightKey = "height";

        private void StageTipHeightAdvance(WriteBatch batch, ulong number)
        {
            if (!_core.HasColumnFamily(RocksDbManager.CF_METADATA)) return;
            var heightKey = System.Text.Encoding.UTF8.GetBytes(HeightKey);
            var currentRaw = _core.Get(RocksDbManager.CF_METADATA, heightKey);
            var current = currentRaw == null ? BigInteger.MinusOne : RocksDbSerializer.BytesToBigInteger(currentRaw);
            if ((BigInteger)number <= current) return;
            var metaCf = _core.GetColumnFamily(RocksDbManager.CF_METADATA);
            batch.Put(heightKey, RocksDbSerializer.BigIntegerToBytes((BigInteger)number), metaCf);
        }

        public void WriteTransaction(ulong blockNumber, int txIndex, ISignedTransaction tx)
        {
            if (tx == null) return;
            using var batch = _core.CreateWriteBatch();
            StageTransaction(batch, blockNumber, txIndex, tx);
            _core.Write(batch);
        }

        public void WriteTransactions(ulong blockNumber, IReadOnlyList<ISignedTransaction> txs)
        {
            if (txs == null || txs.Count == 0) return;
            using var batch = _core.CreateWriteBatch();
            for (int i = 0; i < txs.Count; i++)
                if (txs[i] != null) StageTransaction(batch, blockNumber, i, txs[i]);
            _core.Write(batch);
        }

        private void StageTransaction(WriteBatch batch, ulong blockNumber, int txIndex, ISignedTransaction tx)
        {
            var key = HistoryKeys.TxKey(blockNumber, (uint)txIndex);
            batch.Put(key, _provider.EncodeTransaction(tx), _txBody);
            if (tx.Hash != null) batch.Put(tx.Hash, key, _txHashIndex);
        }

        public void UpdateBlockHash(ulong blockNumber, byte[] newHash)
        {
            if (newHash == null) return;
            var key = HistoryKeys.BlockKey(blockNumber);
            byte[] metaBytes;
            using (var lease = _core.Lease()) metaBytes = lease.Database.Get(key, _meta);
            var meta = BlockMetaCodec.Decode(metaBytes);
            if (meta == null) return;
            using var batch = _core.CreateWriteBatch();
            if (meta.BlockHash != null) batch.Delete(meta.BlockHash, _blockHashIndex);
            meta.BlockHash = newHash;
            batch.Put(key, BlockMetaCodec.Encode(meta), _meta);
            batch.Put(newHash, key, _blockHashIndex);
            _core.Write(batch);
        }


        public void DeleteBlockHeader(ulong blockNumber)
        {
            var key = HistoryKeys.BlockKey(blockNumber);
            byte[] metaBytes;
            using (var lease = _core.Lease()) metaBytes = lease.Database.Get(key, _meta);
            var meta = BlockMetaCodec.Decode(metaBytes);
            using var batch = _core.CreateWriteBatch();
            if (meta?.BlockHash != null) batch.Delete(meta.BlockHash, _blockHashIndex);
            batch.Delete(key, _header);
            batch.Delete(key, _meta);
            _core.Write(batch);
        }

        public void DeleteBlockTransactions(ulong blockNumber)
        {
            var fromKey = HistoryKeys.TxKey(blockNumber, 0);
            var toKey = HistoryKeys.TxKey(blockNumber + 1, 0);
            using var batch = _core.CreateWriteBatch();
            using (var lease = _core.Lease())
            using (var it = lease.Database.NewIterator(_txBody))
            {
                for (it.Seek(fromKey); it.Valid(); it.Next())
                {
                    if (HistoryKeys.ReadBlockNumber(it.Key()) != blockNumber) break;
                    var tx = _provider.DecodeTransaction(it.Value());
                    if (tx?.Hash != null) batch.Delete(tx.Hash, _txHashIndex);
                }
            }
            batch.DeleteRange(fromKey, (ulong)fromKey.Length, toKey, (ulong)toKey.Length, _txBody);
            _core.Write(batch);
        }


        private static readonly byte[] MinBlockBound = new byte[HistoryKeys.BlockKeyLength];
        private static readonly byte[] MinTxBound = new byte[HistoryKeys.TxKeyLength];

        private void StageEvictBelow(WriteBatch batch, ulong floor)
        {
            if (floor == 0) return;
            var floorKey = HistoryKeys.BlockKey(floor);

            using (var lease = _core.Lease())
            using (var it = lease.Database.NewIterator(_meta))
            {
                for (it.SeekToFirst(); it.Valid(); it.Next())
                {
                    if (HistoryKeys.ReadBlockNumber(it.Key()) >= floor) break;
                    var meta = BlockMetaCodec.Decode(it.Value());
                    if (meta?.BlockHash != null) batch.Delete(meta.BlockHash, _blockHashIndex);
                }
            }
            using (var lease = _core.Lease())
            using (var it = lease.Database.NewIterator(_txBody))
            {
                for (it.SeekToFirst(); it.Valid(); it.Next())
                {
                    if (HistoryKeys.ReadBlockNumber(it.Key()) >= floor) break;
                    var tx = _provider.DecodeTransaction(it.Value());
                    if (tx?.Hash != null) batch.Delete(tx.Hash, _txHashIndex);
                }
            }

            batch.DeleteRange(MinBlockBound, (ulong)MinBlockBound.Length, floorKey, (ulong)floorKey.Length, _header);
            batch.DeleteRange(MinBlockBound, (ulong)MinBlockBound.Length, floorKey, (ulong)floorKey.Length, _meta);
            batch.DeleteRange(MinBlockBound, (ulong)MinBlockBound.Length, floorKey, (ulong)floorKey.Length, _blockAccessList);
            var floorTxKey = HistoryKeys.TxKey(floor, 0);
            batch.DeleteRange(MinTxBound, (ulong)MinTxBound.Length, floorTxKey, (ulong)floorTxKey.Length, _txBody);
        }

        public HotBlockPromotionData ReadForPromotion(ulong number)
        {
            var key = HistoryKeys.BlockKey(number);
            byte[] headerBytes, metaBytes, accessListBytes;
            using (var lease = _core.Lease())
            {
                headerBytes = lease.Database.Get(key, _header);
                metaBytes = lease.Database.Get(key, _meta);
                accessListBytes = lease.Database.Get(key, _blockAccessList);
            }
            var meta = BlockMetaCodec.Decode(metaBytes);
            if (headerBytes == null || meta?.BlockHash == null) return null;
            return new HotBlockPromotionData
            {
                Header = _provider.DecodeBlockHeader(headerBytes),
                BlockHash = meta.BlockHash,
                Meta = meta,
                Transactions = GetTransactionsForBlock(number),
                Receipts = ReadReceiptsForBlock(number),
                BlockAccessList = accessListBytes,
            };
        }

        private List<ReceiptInfo> ReadReceiptsForBlock(ulong number)
        {
            var result = new List<ReceiptInfo>();
            using var lease = _core.Lease();
            using var it = lease.Database.NewIterator(_receiptBody);
            for (it.Seek(HistoryKeys.TxKey(number, 0)); it.Valid(); it.Next())
            {
                if (HistoryKeys.ReadBlockNumber(it.Key()) != number) break;
                var info = _serializer.DeserializeReceiptInfoWith(it.Value());
                if (info != null) result.Add(info);
            }
            return result;
        }

        public void RangeDeleteAtOrBelow(WriteBatch batch, ulong number)
        {
            var upperExclusive = HistoryKeys.BlockKey(number + 1);
            using (var lease = _core.Lease())
            using (var it = lease.Database.NewIterator(_meta))
            {
                for (it.SeekToFirst(); it.Valid(); it.Next())
                {
                    if (HistoryKeys.ReadBlockNumber(it.Key()) > number) break;
                    var meta = BlockMetaCodec.Decode(it.Value());
                    if (meta?.BlockHash != null) batch.Delete(meta.BlockHash, _blockHashIndex);
                }
            }
            using (var lease = _core.Lease())
            using (var it = lease.Database.NewIterator(_txBody))
            {
                for (it.SeekToFirst(); it.Valid(); it.Next())
                {
                    if (HistoryKeys.ReadBlockNumber(it.Key()) > number) break;
                    var tx = _provider.DecodeTransaction(it.Value());
                    if (tx?.Hash != null) batch.Delete(tx.Hash, _txHashIndex);
                }
            }
            batch.DeleteRange(MinBlockBound, (ulong)MinBlockBound.Length, upperExclusive, (ulong)upperExclusive.Length, _header);
            batch.DeleteRange(MinBlockBound, (ulong)MinBlockBound.Length, upperExclusive, (ulong)upperExclusive.Length, _meta);
            batch.DeleteRange(MinBlockBound, (ulong)MinBlockBound.Length, upperExclusive, (ulong)upperExclusive.Length, _blockAccessList);
            var upperTxExclusive = HistoryKeys.TxKey(number + 1, 0);
            batch.DeleteRange(MinTxBound, (ulong)MinTxBound.Length, upperTxExclusive, (ulong)upperTxExclusive.Length, _txBody);
            batch.DeleteRange(MinTxBound, (ulong)MinTxBound.Length, upperTxExclusive, (ulong)upperTxExclusive.Length, _receiptBody);
        }


        public bool ContainsBlock(ulong number)
        {
            using var lease = _core.Lease();
            return lease.Database.Get(HistoryKeys.BlockKey(number), _header) != null;
        }

        public BlockHeader TryGetHeader(ulong number)
        {
            byte[] b;
            using (var lease = _core.Lease()) b = lease.Database.Get(HistoryKeys.BlockKey(number), _header);
            return b == null ? null : _provider.DecodeBlockHeader(b);
        }

        public byte[] TryGetHash(ulong number)
        {
            using var lease = _core.Lease();
            return BlockMetaCodec.Decode(lease.Database.Get(HistoryKeys.BlockKey(number), _meta))?.BlockHash;
        }

        public bool ContainsBlockHash(byte[] hash) => hash != null && TryGetBlockNumberByHash(hash).HasValue;

        public ulong? TryGetBlockNumberByHash(byte[] hash)
        {
            if (hash == null) return null;
            using var lease = _core.Lease();
            var loc = lease.Database.Get(hash, _blockHashIndex);
            return loc == null || loc.Length < HistoryKeys.BlockKeyLength ? (ulong?)null : HistoryKeys.ReadBlockNumber(loc);
        }

        public BlockHeader TryGetHeaderByHash(byte[] hash)
        {
            var n = TryGetBlockNumberByHash(hash);
            return n.HasValue ? TryGetHeader(n.Value) : null;
        }

        public ulong? TryGetLatestNumber()
        {
            using var lease = _core.Lease();
            using var it = lease.Database.NewIterator(_header);
            it.SeekToLast();
            if (!it.Valid()) return null;
            var key = it.Key();
            return key.Length < HistoryKeys.BlockKeyLength ? (ulong?)null : HistoryKeys.ReadBlockNumber(key);
        }

        public List<ISignedTransaction> GetTransactionsForBlock(ulong number)
        {
            var result = new List<ISignedTransaction>();
            using var lease = _core.Lease();
            using var it = lease.Database.NewIterator(_txBody);
            for (it.Seek(HistoryKeys.TxKey(number, 0)); it.Valid(); it.Next())
            {
                if (HistoryKeys.ReadBlockNumber(it.Key()) != number) break;
                result.Add(_provider.DecodeTransaction(it.Value()));
            }
            return result;
        }

        public List<byte[]> GetTransactionHashesForBlock(ulong number)
        {
            var result = new List<byte[]>();
            using var lease = _core.Lease();
            using var it = lease.Database.NewIterator(_txBody);
            for (it.Seek(HistoryKeys.TxKey(number, 0)); it.Valid(); it.Next())
            {
                if (HistoryKeys.ReadBlockNumber(it.Key()) != number) break;
                result.Add(_provider.DecodeTransaction(it.Value()).Hash);
            }
            return result;
        }

        public (ulong Block, uint Index)? TryGetTransactionLocation(byte[] txHash)
        {
            if (txHash == null) return null;
            using var lease = _core.Lease();
            var loc = lease.Database.Get(txHash, _txHashIndex);
            if (loc == null || loc.Length < HistoryKeys.TxKeyLength) return null;
            return (HistoryKeys.ReadBlockNumber(loc), HistoryKeys.ReadTxIndex(loc));
        }

        public ISignedTransaction TryGetTransactionByHash(byte[] txHash)
        {
            var loc = TryGetTransactionLocation(txHash);
            if (!loc.HasValue) return null;
            byte[] b;
            using (var lease = _core.Lease()) b = lease.Database.Get(HistoryKeys.TxKey(loc.Value.Block, loc.Value.Index), _txBody);
            return b == null ? null : _provider.DecodeTransaction(b);
        }
    }
}
