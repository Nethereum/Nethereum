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

        public Task<BigInteger> GetHeightAsync()
        {
            var heightData = _manager.Get(RocksDbManager.CF_METADATA, Encoding.UTF8.GetBytes(HEIGHT_KEY));
            if (heightData == null) return Task.FromResult(BigInteger.MinusOne);

            var height = RocksDbSerializer.BytesToBigInteger(heightData);
            return Task.FromResult(height);
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

            var currentHeight = GetHeightAsync().Result;
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

        private static byte[] GetBlockNumberKey(BigInteger number)
        {
            var bytes = number.ToByteArray();
            var padded = new byte[32];
            if (bytes.Length <= 32)
            {
                System.Buffer.BlockCopy(bytes, 0, padded, 32 - bytes.Length, bytes.Length);
            }
            return padded;
        }
    }
}
