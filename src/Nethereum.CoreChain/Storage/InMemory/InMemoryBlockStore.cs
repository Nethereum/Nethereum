using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage.InMemory
{
    public class InMemoryBlockStore : IBlockStore
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, BlockHeader> _blocksByHash = new Dictionary<string, BlockHeader>();
        private readonly Dictionary<BigInteger, string> _hashByNumber = new Dictionary<BigInteger, string>();
        private BigInteger _latestBlockNumber = -1;

        public Task<BlockHeader> GetByHashAsync(byte[] hash)
        {
            lock (_lock)
            {
                var hashHex = ToHex(hash);
                _blocksByHash.TryGetValue(hashHex, out var header);
                return Task.FromResult(header);
            }
        }

        public Task<BlockHeader> GetByNumberAsync(BigInteger number)
        {
            lock (_lock)
            {
                if (!_hashByNumber.TryGetValue(number, out var hashHex))
                    return Task.FromResult<BlockHeader>(null);

                _blocksByHash.TryGetValue(hashHex, out var header);
                return Task.FromResult(header);
            }
        }

        public Task<BlockHeader> GetLatestAsync()
        {
            lock (_lock)
            {
                if (_latestBlockNumber < 0)
                    return Task.FromResult<BlockHeader>(null);

                return GetByNumberAsync(_latestBlockNumber);
            }
        }

        public Task<BigInteger> GetHeightAsync()
        {
            lock (_lock)
            {
                return Task.FromResult(_latestBlockNumber);
            }
        }

        public Task SaveAsync(BlockHeader header, byte[] blockHash)
        {
            lock (_lock)
            {
                var hashHex = ToHex(blockHash);
                _blocksByHash[hashHex] = header;
                _hashByNumber[header.BlockNumber] = hashHex;

                if (header.BlockNumber > _latestBlockNumber)
                    _latestBlockNumber = header.BlockNumber;
            }
            return Task.FromResult(0);
        }

        public Task<bool> ExistsAsync(byte[] hash)
        {
            lock (_lock)
            {
                var hashHex = ToHex(hash);
                return Task.FromResult(_blocksByHash.ContainsKey(hashHex));
            }
        }

        public Task<byte[]> GetHashByNumberAsync(BigInteger number)
        {
            lock (_lock)
            {
                if (!_hashByNumber.TryGetValue(number, out var hashHex))
                    return Task.FromResult<byte[]>(null);

                return Task.FromResult(FromHex(hashHex));
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _blocksByHash.Clear();
                _hashByNumber.Clear();
                _latestBlockNumber = -1;
            }
        }

        private static string ToHex(byte[] bytes)
        {
            if (bytes == null) return null;
            return System.BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        private static byte[] FromHex(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return null;
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = System.Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
    }
}
