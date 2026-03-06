using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB.Serialization;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public class RocksDbBlockStore : IBlockStore
    {
        private const string LATEST_BLOCK_KEY = "latest_block_hash";
        private const string HEIGHT_KEY = "height";

        private readonly RocksDbManager _manager;

        public RocksDbBlockStore(RocksDbManager manager)
        {
            _manager = manager;
        }

        public Task<BlockHeader> GetByHashAsync(byte[] hash)
        {
            if (hash == null) return Task.FromResult<BlockHeader>(null);

            var data = _manager.Get(RocksDbManager.CF_BLOCKS, hash);
            var header = RocksDbSerializer.DeserializeBlockHeader(data);
            return Task.FromResult(header);
        }

        public Task<BlockHeader> GetByNumberAsync(BigInteger number)
        {
            var hashKey = GetBlockNumberKey(number);
            var hash = _manager.Get(RocksDbManager.CF_BLOCK_NUMBERS, hashKey);

            if (hash == null) return Task.FromResult<BlockHeader>(null);

            return GetByHashAsync(hash);
        }

        public Task<BlockHeader> GetLatestAsync()
        {
            var latestHashData = _manager.Get(RocksDbManager.CF_METADATA, Encoding.UTF8.GetBytes(LATEST_BLOCK_KEY));
            if (latestHashData == null) return Task.FromResult<BlockHeader>(null);

            return GetByHashAsync(latestHashData);
        }

        public Task<BigInteger> GetHeightAsync() => Task.FromResult(GetHeightInternal());

        private BigInteger GetHeightInternal()
        {
            var heightData = _manager.Get(RocksDbManager.CF_METADATA, Encoding.UTF8.GetBytes(HEIGHT_KEY));
            if (heightData == null) return BigInteger.MinusOne;
            return RocksDbSerializer.BytesToBigInteger(heightData);
        }

        public Task SaveAsync(BlockHeader header, byte[] blockHash)
        {
            if (header == null || blockHash == null) return Task.CompletedTask;

            using var batch = _manager.CreateWriteBatch();
            var blocksCf = _manager.GetColumnFamily(RocksDbManager.CF_BLOCKS);
            var blockNumbersCf = _manager.GetColumnFamily(RocksDbManager.CF_BLOCK_NUMBERS);
            var metadataCf = _manager.GetColumnFamily(RocksDbManager.CF_METADATA);

            var headerData = RocksDbSerializer.SerializeBlockHeader(header);
            batch.Put(blockHash, headerData, blocksCf);

            var numberKey = GetBlockNumberKey(header.BlockNumber);
            batch.Put(numberKey, blockHash, blockNumbersCf);

            batch.Put(Encoding.UTF8.GetBytes(LATEST_BLOCK_KEY), blockHash, metadataCf);

            var currentHeight = GetHeightInternal();
            if (header.BlockNumber > currentHeight)
            {
                var heightBytes = RocksDbSerializer.BigIntegerToBytes(header.BlockNumber);
                batch.Put(Encoding.UTF8.GetBytes(HEIGHT_KEY), heightBytes, metadataCf);
            }

            _manager.Write(batch);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(byte[] hash)
        {
            if (hash == null) return Task.FromResult(false);
            var exists = _manager.KeyExists(RocksDbManager.CF_BLOCKS, hash);
            return Task.FromResult(exists);
        }

        public Task<byte[]> GetHashByNumberAsync(BigInteger number)
        {
            var hashKey = GetBlockNumberKey(number);
            var hash = _manager.Get(RocksDbManager.CF_BLOCK_NUMBERS, hashKey);
            return Task.FromResult(hash);
        }

        public Task UpdateBlockHashAsync(BigInteger blockNumber, byte[] newHash)
        {
            var hashKey = GetBlockNumberKey(blockNumber);
            var oldHash = _manager.Get(RocksDbManager.CF_BLOCK_NUMBERS, hashKey);

            if (oldHash == null || newHash == null) return Task.CompletedTask;

            var headerData = _manager.Get(RocksDbManager.CF_BLOCKS, oldHash);
            if (headerData == null) return Task.CompletedTask;

            using var batch = _manager.CreateWriteBatch();
            var blocksCf = _manager.GetColumnFamily(RocksDbManager.CF_BLOCKS);
            var blockNumbersCf = _manager.GetColumnFamily(RocksDbManager.CF_BLOCK_NUMBERS);

            batch.Delete(oldHash, blocksCf);
            batch.Put(newHash, headerData, blocksCf);
            batch.Put(hashKey, newHash, blockNumbersCf);

            _manager.Write(batch);
            return Task.CompletedTask;
        }

        public Task DeleteByNumberAsync(BigInteger blockNumber)
        {
            var hashKey = GetBlockNumberKey(blockNumber);
            var hash = _manager.Get(RocksDbManager.CF_BLOCK_NUMBERS, hashKey);
            if (hash == null) return Task.CompletedTask;

            using var batch = _manager.CreateWriteBatch();
            var blocksCf = _manager.GetColumnFamily(RocksDbManager.CF_BLOCKS);
            var blockNumbersCf = _manager.GetColumnFamily(RocksDbManager.CF_BLOCK_NUMBERS);
            var metadataCf = _manager.GetColumnFamily(RocksDbManager.CF_METADATA);

            batch.Delete(hash, blocksCf);
            batch.Delete(hashKey, blockNumbersCf);

            var currentHeight = GetHeightInternal();
            if (blockNumber == currentHeight)
            {
                var newHeight = blockNumber - 1;
                var heightBytes = RocksDbSerializer.BigIntegerToBytes(newHeight);
                batch.Put(Encoding.UTF8.GetBytes(HEIGHT_KEY), heightBytes, metadataCf);

                var prevHashKey = GetBlockNumberKey(newHeight);
                var prevHash = _manager.Get(RocksDbManager.CF_BLOCK_NUMBERS, prevHashKey);
                if (prevHash != null)
                {
                    batch.Put(Encoding.UTF8.GetBytes(LATEST_BLOCK_KEY), prevHash, metadataCf);
                }
            }

            _manager.Write(batch);
            return Task.CompletedTask;
        }

        private static byte[] GetBlockNumberKey(BigInteger number)
        {
            var bytes = number.ToByteArray(isUnsigned: true, isBigEndian: true);
            var padded = new byte[32];
            if (bytes.Length <= 32)
            {
                System.Buffer.BlockCopy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
            }
            return padded;
        }
    }
}
