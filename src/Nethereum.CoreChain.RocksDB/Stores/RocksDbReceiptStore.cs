using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Serialization;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.History;
using Nethereum.Model;
using Nethereum.Util;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public class RocksDbReceiptStore : IReceiptStore
    {
        private readonly RocksDbManager _manager;
        private readonly IBlockStore _blockStore;
        private readonly RocksDbSerializer _serializer;
        private readonly ColumnFamilyHandle _receiptBody, _txHashIndex;
        private readonly bool _writeTxHashIndex;

        public RocksDbReceiptStore(RocksDbManager manager, IBlockStore blockStore = null, RocksDbSerializer serializer = null,
            string receiptBodyCf = null, string txHashIndexCf = null, bool writeTxHashIndex = true)
        {
            _manager = manager;
            _blockStore = blockStore;
            _serializer = serializer ?? RocksDbSerializer.Default;
            _receiptBody = manager.GetColumnFamily(receiptBodyCf ?? HistoryColumnFamilies.ReceiptBody);
            _txHashIndex = manager.GetColumnFamily(txHashIndexCf ?? HistoryColumnFamilies.TxHashIndex);
            _writeTxHashIndex = writeTxHashIndex;
        }

        private ReceiptInfo InfoAt(ulong n, uint i)
        {
            using var lease = _manager.Lease();
            var b = lease.Database.Get(HistoryKeys.TxKey(n, i), _receiptBody);
            return b == null ? null : _serializer.DeserializeReceiptInfoWith(b);
        }

        private ReceiptInfo InfoByHash(byte[] txHash)
        {
            byte[] loc;
            using (var lease = _manager.Lease()) loc = lease.Database.Get(txHash, _txHashIndex);
            if (loc == null || loc.Length < HistoryKeys.TxKeyLength) return null;
            return InfoAt(HistoryKeys.ReadBlockNumber(loc), HistoryKeys.ReadTxIndex(loc));
        }

        public Task<Receipt> GetByTxHashAsync(byte[] txHash)
            => Task.FromResult(txHash == null ? null : InfoByHash(txHash)?.Receipt);

        public Task<ReceiptInfo> GetInfoByTxHashAsync(byte[] txHash)
            => Task.FromResult(txHash == null ? null : InfoByHash(txHash));

        public Task<List<Receipt>> GetByBlockNumberAsync(BigInteger blockNumber)
        {
            var result = new List<Receipt>();
            var n = (ulong)blockNumber;
            using var lease = _manager.Lease();
            using var it = lease.Database.NewIterator(_receiptBody);
            for (it.Seek(HistoryKeys.TxKey(n, 0)); it.Valid(); it.Next())
            {
                if (HistoryKeys.ReadBlockNumber(it.Key()) != n) break;
                var info = _serializer.DeserializeReceiptInfoWith(it.Value());
                if (info != null) result.Add(info.Receipt);
            }
            return Task.FromResult(result);
        }

        public async Task<List<Receipt>> GetByBlockHashAsync(byte[] blockHash)
        {
            var n = await NumberOf(blockHash).ConfigureAwait(false);
            return n.HasValue ? await GetByBlockNumberAsync(n.Value).ConfigureAwait(false) : new List<Receipt>();
        }

        public Task SaveAsync(Receipt receipt, byte[] txHash, byte[] blockHash, BigInteger blockNumber, int txIndex, BigInteger gasUsed, string contractAddress, BigInteger effectiveGasPrice)
        {
            if (receipt == null || txHash == null) return Task.CompletedTask;
            var key = HistoryKeys.TxKey((ulong)blockNumber, (uint)txIndex);
            using var batch = _manager.CreateWriteBatch();
            batch.Put(key, _serializer.SerializeReceiptInfoWith(new ReceiptInfo
            {
                Receipt = receipt, TxHash = txHash, BlockHash = blockHash, BlockNumber = blockNumber,
                TransactionIndex = txIndex, GasUsed = gasUsed, ContractAddress = contractAddress, EffectiveGasPrice = effectiveGasPrice,
            }), _receiptBody);
            if (_writeTxHashIndex) batch.Put(txHash, key, _txHashIndex);
            _manager.Write(batch);
            return Task.CompletedTask;
        }

        public Task SaveManyAsync(byte[] blockHash, BigInteger blockNumber, IReadOnlyList<ReceiptSaveItem> items)
        {
            if (items == null || items.Count == 0) return Task.CompletedTask;
            using var batch = _manager.CreateWriteBatch();
            StageManyInto(batch, blockHash, blockNumber, items);
            _manager.Write(batch);
            return Task.CompletedTask;
        }

        public void StageManyInto(WriteBatch batch, byte[] blockHash, BigInteger blockNumber, IReadOnlyList<ReceiptSaveItem> items)
        {
            if (items == null || items.Count == 0) return;
            var n = (ulong)blockNumber;
            foreach (var it in items)
            {
                if (it.Receipt == null || it.TxHash == null) continue;
                var key = HistoryKeys.TxKey(n, (uint)it.TxIndex);
                batch.Put(key, _serializer.SerializeReceiptInfoWith(new ReceiptInfo
                {
                    Receipt = it.Receipt, TxHash = it.TxHash, BlockHash = blockHash, BlockNumber = blockNumber,
                    TransactionIndex = it.TxIndex, GasUsed = it.GasUsed, ContractAddress = it.ContractAddress, EffectiveGasPrice = it.EffectiveGasPrice,
                }), _receiptBody);
                if (_writeTxHashIndex) batch.Put(it.TxHash, key, _txHashIndex);
            }
        }

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber)
        {
            if (blockNumber < 0) return Task.CompletedTask;
            var fromKey = HistoryKeys.TxKey((ulong)blockNumber, 0);
            using var batch = _manager.CreateWriteBatch();
            batch.DeleteRange(fromKey, (ulong)fromKey.Length, MaxTxBound, (ulong)MaxTxBound.Length, _receiptBody);
            _manager.Write(batch);
            return Task.CompletedTask;
        }

        private async Task<ulong?> NumberOf(byte[] blockHash)
        {
            if (blockHash == null || _blockStore == null) return null;
            var header = await _blockStore.GetByHashAsync(blockHash).ConfigureAwait(false);
            return header == null ? (ulong?)null : (ulong)header.BlockNumber.ToBigInteger();
        }

        private static readonly byte[] MaxTxBound =
            { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff };
    }
}
