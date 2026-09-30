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
    public class RocksDbTransactionStore : ITransactionStore
    {
        private readonly RocksDbManager _manager;
        private readonly IBlockStore _blockStore;
        private readonly IBlockEncodingProvider _provider;
        private readonly IHistoryReorgDecoder _reorg;
        private readonly ColumnFamilyHandle _txBody, _txHashIndex;

        public RocksDbTransactionStore(
            RocksDbManager manager,
            IBlockStore blockStore = null,
            IBlockEncodingProvider provider = null,
            IHistoryReorgDecoder reorgDecoder = null)
        {
            _manager = manager;
            _blockStore = blockStore;
            _provider = provider ?? RlpBlockEncodingProvider.Instance;
            _reorg = reorgDecoder ?? new HistoryReorgDecoder(_provider);
            _txBody = manager.GetColumnFamily(HistoryColumnFamilies.TxBody);
            _txHashIndex = manager.GetColumnFamily(HistoryColumnFamilies.TxHashIndex);
        }

        private TxLoc? Find(byte[] txHash)
        {
            using var lease = _manager.Lease();
            var loc = lease.Database.Get(txHash, _txHashIndex);
            if (loc == null || loc.Length < HistoryKeys.TxKeyLength) return null;
            return new TxLoc(HistoryKeys.ReadBlockNumber(loc), HistoryKeys.ReadTxIndex(loc));
        }

        public Task<ISignedTransaction> GetByHashAsync(byte[] txHash)
        {
            if (txHash == null) return Task.FromResult<ISignedTransaction>(null);
            var loc = Find(txHash);
            if (!loc.HasValue) return Task.FromResult<ISignedTransaction>(null);
            using var lease = _manager.Lease();
            var b = lease.Database.Get(HistoryKeys.TxKey(loc.Value.Block, loc.Value.Index), _txBody);
            return Task.FromResult(b == null ? null : _provider.DecodeTransaction(b));
        }

        public Task<List<ISignedTransaction>> GetByBlockNumberAsync(BigInteger blockNumber)
        {
            var result = new List<ISignedTransaction>();
            var n = (ulong)blockNumber;
            using var lease = _manager.Lease();
            using var it = lease.Database.NewIterator(_txBody);
            for (it.Seek(HistoryKeys.TxKey(n, 0)); it.Valid(); it.Next())
            {
                if (HistoryKeys.ReadBlockNumber(it.Key()) != n) break;
                result.Add(_provider.DecodeTransaction(it.Value()));
            }
            return Task.FromResult(result);
        }

        public async Task<List<ISignedTransaction>> GetByBlockHashAsync(byte[] blockHash)
        {
            var n = await NumberOf(blockHash).ConfigureAwait(false);
            return n.HasValue ? await GetByBlockNumberAsync(n.Value).ConfigureAwait(false) : new List<ISignedTransaction>();
        }

        public async Task<List<byte[]>> GetHashesByBlockHashAsync(byte[] blockHash)
        {
            var result = new List<byte[]>();
            var n = await NumberOf(blockHash).ConfigureAwait(false);
            if (!n.HasValue) return result;
            using var lease = _manager.Lease();
            using var it = lease.Database.NewIterator(_txBody);
            for (it.Seek(HistoryKeys.TxKey((ulong)n.Value, 0)); it.Valid(); it.Next())
            {
                if (HistoryKeys.ReadBlockNumber(it.Key()) != (ulong)n.Value) break;
                result.Add(_provider.DecodeTransaction(it.Value()).Hash);
            }
            return result;
        }

        public async Task<TransactionLocation> GetLocationAsync(byte[] txHash)
        {
            if (txHash == null) return null;
            var loc = Find(txHash);
            if (!loc.HasValue) return null;
            var blockHash = _blockStore == null ? null : await _blockStore.GetHashByNumberAsync(loc.Value.Block).ConfigureAwait(false);
            return new TransactionLocation { BlockHash = blockHash, BlockNumber = loc.Value.Block, TransactionIndex = (int)loc.Value.Index };
        }

        public Task SaveAsync(ISignedTransaction tx, byte[] blockHash, int txIndex, BigInteger blockNumber)
        {
            if (tx == null) return Task.CompletedTask;
            using var batch = _manager.CreateWriteBatch();
            StageOne(batch, tx, txIndex, (ulong)blockNumber);
            _manager.Write(batch);
            return Task.CompletedTask;
        }

        public Task SaveManyAsync(byte[] blockHash, BigInteger blockNumber, IReadOnlyList<ISignedTransaction> txs)
        {
            if (txs == null || txs.Count == 0) return Task.CompletedTask;
            using var batch = _manager.CreateWriteBatch();
            StageManyInto(batch, blockHash, blockNumber, txs);
            _manager.Write(batch);
            return Task.CompletedTask;
        }

        public void StageManyInto(WriteBatch batch, byte[] blockHash, BigInteger blockNumber, IReadOnlyList<ISignedTransaction> txs)
        {
            if (txs == null || txs.Count == 0) return;
            var n = (ulong)blockNumber;
            for (int i = 0; i < txs.Count; i++)
                if (txs[i] != null) StageOne(batch, txs[i], i, n);
        }

        private void StageOne(WriteBatch batch, ISignedTransaction tx, int txIndex, ulong blockNumber)
        {
            var key = HistoryKeys.TxKey(blockNumber, (uint)txIndex);
            batch.Put(key, _provider.EncodeTransaction(tx), _txBody);
            if (tx.Hash != null) batch.Put(tx.Hash, key, _txHashIndex);
        }

        public Task DeleteByBlockNumberAsync(BigInteger blockNumber)
        {
            if (blockNumber < 0) return Task.CompletedTask;
            var fromKey = HistoryKeys.TxKey((ulong)blockNumber, 0);
            using var batch = _manager.CreateWriteBatch();
            using (var lease = _manager.Lease())
            using (var it = lease.Database.NewIterator(_txBody))
                for (it.Seek(fromKey); it.Valid(); it.Next())
                {
                    var h = _reorg.TxHash(it.Value());
                    if (h != null) batch.Delete(h, _txHashIndex);
                }
            batch.DeleteRange(fromKey, (ulong)fromKey.Length, MaxTxBound, (ulong)MaxTxBound.Length, _txBody);
            _manager.Write(batch);
            return Task.CompletedTask;
        }

        private async Task<ulong?> NumberOf(byte[] blockHash)
        {
            if (blockHash == null || _blockStore == null) return null;
            var header = await _blockStore.GetByHashAsync(blockHash).ConfigureAwait(false);
            return header == null ? (ulong?)null : (ulong)header.BlockNumber.ToBigInteger();
        }

        private readonly struct TxLoc
        {
            public readonly ulong Block; public readonly uint Index;
            public TxLoc(ulong b, uint i) { Block = b; Index = i; }
        }

        private static readonly byte[] MaxTxBound =
            { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff };
    }
}
