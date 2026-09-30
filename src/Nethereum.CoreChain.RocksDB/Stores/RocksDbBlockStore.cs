using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.History;
using Nethereum.CoreChain.RocksDB.Serialization;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.History;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public class RocksDbBlockStore : IBlockStore
    {
        private readonly RocksDbManager _manager;
        private readonly IBlockEncodingProvider _provider;
        private readonly IHistoryReorgDecoder _reorg;
        private readonly ColumnFamilyHandle _blockHeader, _blockMeta, _blockHashIndex;

        public RocksDbBlockStore(RocksDbManager manager, RocksDbSerializer serializer = null,
            IBlockEncodingProvider provider = null, IHistoryReorgDecoder reorgDecoder = null)
        {
            _manager = manager;
            _provider = provider ?? RlpBlockEncodingProvider.Instance;
            _reorg = reorgDecoder ?? new HistoryReorgDecoder(_provider);
            _blockHeader = manager.GetColumnFamily(HistoryColumnFamilies.BlockHeader);
            _blockMeta = manager.GetColumnFamily(HistoryColumnFamilies.BlockMeta);
            _blockHashIndex = manager.GetColumnFamily(HistoryColumnFamilies.BlockHashIndex);
        }

        private BlockHeader Decode(byte[] b) => b == null ? null : _provider.DecodeBlockHeader(b);

        public Task<BlockHeader> GetByNumberAsync(BigInteger number)
        {
            using var lease = _manager.Lease();
            return Task.FromResult(Decode(lease.Database.Get(HistoryKeys.BlockKey((ulong)number), _blockHeader)));
        }

        public Task<BlockHeader> GetByHashAsync(byte[] hash)
        {
            var n = FindBlock(hash);
            using var lease = _manager.Lease();
            return Task.FromResult(n.HasValue ? Decode(lease.Database.Get(HistoryKeys.BlockKey(n.Value), _blockHeader)) : null);
        }

        public Task<byte[]> GetHashByNumberAsync(BigInteger number)
        {
            using var lease = _manager.Lease();
            return Task.FromResult(BlockMetaCodec.Decode(lease.Database.Get(HistoryKeys.BlockKey((ulong)number), _blockMeta))?.BlockHash);
        }

        public Task<bool> ExistsAsync(byte[] hash) => Task.FromResult(hash != null && FindBlock(hash).HasValue);

        public Task<BlockHeader> GetLatestAsync()
        {
            var tip = GetTip();
            using var lease = _manager.Lease();
            return Task.FromResult(tip.HasValue ? Decode(lease.Database.Get(HistoryKeys.BlockKey(tip.Value), _blockHeader)) : null);
        }

        public Task<BigInteger> GetHeightAsync()
        {
            var tip = GetTip();
            return Task.FromResult(tip.HasValue ? (BigInteger)tip.Value : BigInteger.MinusOne);
        }

        public Task SaveAsync(BlockHeader header, byte[] blockHash)
        {
            if (header == null || blockHash == null) return Task.CompletedTask;
            using var batch = _manager.CreateWriteBatch();
            StageInto(batch, header, blockHash);
            _manager.Write(batch);
            return Task.CompletedTask;
        }

        public void StageInto(WriteBatch batch, BlockHeader header, byte[] blockHash)
            => StageBlockInto(batch, header, blockHash, null, null, txCount: 0);

        public void StageBlockInto(WriteBatch batch, BlockHeader header, byte[] blockHash,
            IList<BlockHeader> uncles, IList<Withdrawal> withdrawals, int txCount)
        {
            if (header == null || blockHash == null) return;
            StageBlockMetaInto(batch, header, blockHash, new BlockMeta
            {
                BlockHash = blockHash,
                TxCount = txCount,
                Uncles = EncodeUncles(uncles),
                Withdrawals = EncodeWithdrawals(withdrawals),
                Bloom = header.LogsBloom ?? System.Array.Empty<byte>(),
            });
        }

        public void StageBlockMetaInto(WriteBatch batch, BlockHeader header, byte[] blockHash, BlockMeta meta)
        {
            if (header == null || blockHash == null || meta == null) return;
            var key = HistoryKeys.BlockKey((ulong)header.BlockNumber.ToBigInteger());
            batch.Put(key, _provider.EncodeBlockHeader(header), _blockHeader);
            batch.Put(key, BlockMetaCodec.Encode(meta), _blockMeta);
            batch.Put(blockHash, key, _blockHashIndex);

            if (_manager.HasColumnFamily(RocksDbManager.CF_METADATA))
            {
                var currentHeight = CurrentHeight();
                var thisNumber = header.BlockNumber.ToBigInteger();
                if (thisNumber > currentHeight)
                {
                    var metaCf = _manager.GetColumnFamily(RocksDbManager.CF_METADATA);
                    batch.Put(System.Text.Encoding.UTF8.GetBytes(LatestBlockKey), blockHash, metaCf);
                    batch.Put(System.Text.Encoding.UTF8.GetBytes(HeightKey), RocksDbSerializer.BigIntegerToBytes(thisNumber), metaCf);
                }
            }
        }

        private const string LatestBlockKey = "latest_block_hash";
        private const string HeightKey = "height";

        private BigInteger CurrentHeight()
        {
            var h = _manager.Get(RocksDbManager.CF_METADATA, System.Text.Encoding.UTF8.GetBytes(HeightKey));
            return h == null ? BigInteger.MinusOne : RocksDbSerializer.BytesToBigInteger(h);
        }

        private byte[] EncodeUncles(IList<BlockHeader> uncles)
        {
            var n = uncles?.Count ?? 0;
            var encoded = new byte[n][];
            for (int i = 0; i < n; i++) encoded[i] = _provider.EncodeBlockHeader(uncles[i]);
            return RLP.RLP.EncodeList(encoded);
        }

        private static byte[] EncodeWithdrawals(IList<Withdrawal> withdrawals)
        {
            if (withdrawals == null) return null;
            var encoded = new byte[withdrawals.Count][];
            for (int i = 0; i < withdrawals.Count; i++) encoded[i] = WithdrawalEncoder.Current.Encode(withdrawals[i]);
            return RLP.RLP.EncodeList(encoded);
        }

        public Task UpdateBlockHashAsync(BigInteger blockNumber, byte[] newHash)
        {
            var key = HistoryKeys.BlockKey((ulong)blockNumber);
            byte[] metaBytes;
            using (var lease = _manager.Lease()) metaBytes = lease.Database.Get(key, _blockMeta);
            var meta = BlockMetaCodec.Decode(metaBytes);
            if (meta == null || newHash == null) return Task.CompletedTask;

            using var batch = _manager.CreateWriteBatch();
            if (meta.BlockHash != null) batch.Delete(meta.BlockHash, _blockHashIndex);
            meta.BlockHash = newHash;
            batch.Put(key, BlockMetaCodec.Encode(meta), _blockMeta);
            batch.Put(newHash, key, _blockHashIndex);
            _manager.Write(batch);
            return Task.CompletedTask;
        }

        public Task DeleteByNumberAsync(BigInteger blockNumber)
        {
            if (blockNumber <= 0) return Task.CompletedTask;
            var from = (ulong)blockNumber;
            var fromKey = HistoryKeys.BlockKey(from);

            using var batch = _manager.CreateWriteBatch();
            using (var lease = _manager.Lease())
            using (var it = lease.Database.NewIterator(_blockMeta))
                for (it.Seek(fromKey); it.Valid(); it.Next())
                {
                    var h = _reorg.BlockHash(it.Value());
                    if (h != null) batch.Delete(h, _blockHashIndex);
                }
            batch.DeleteRange(fromKey, (ulong)fromKey.Length, MaxBlockBound, (ulong)MaxBlockBound.Length, _blockHeader);
            batch.DeleteRange(fromKey, (ulong)fromKey.Length, MaxBlockBound, (ulong)MaxBlockBound.Length, _blockMeta);

            var newHead = from - 1;
            if (_manager.HasColumnFamily(RocksDbManager.CF_METADATA))
            {
                var metaCf = _manager.GetColumnFamily(RocksDbManager.CF_METADATA);
                batch.Put(System.Text.Encoding.UTF8.GetBytes(HeightKey), RocksDbSerializer.BigIntegerToBytes(newHead), metaCf);
                byte[] prevMeta;
                using (var lease = _manager.Lease()) prevMeta = lease.Database.Get(HistoryKeys.BlockKey(newHead), _blockMeta);
                var prevHash = BlockMetaCodec.Decode(prevMeta)?.BlockHash;
                if (prevHash != null) batch.Put(System.Text.Encoding.UTF8.GetBytes(LatestBlockKey), prevHash, metaCf);
            }

            _manager.Write(batch);
            return Task.CompletedTask;
        }

        private ulong? GetTip()
        {
            using var lease = _manager.Lease();
            using var it = lease.Database.NewIterator(_blockHeader);
            it.SeekToLast();
            if (!it.Valid()) return null;
            var key = it.Key();
            return key.Length < HistoryKeys.BlockKeyLength ? (ulong?)null : HistoryKeys.ReadBlockNumber(key);
        }

        private ulong? FindBlock(byte[] hash)
        {
            using var lease = _manager.Lease();
            var num = lease.Database.Get(hash, _blockHashIndex);
            return num == null || num.Length < HistoryKeys.BlockKeyLength ? (ulong?)null : HistoryKeys.ReadBlockNumber(num);
        }

        private static readonly byte[] MaxBlockBound =
            { 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff };
    }
}
