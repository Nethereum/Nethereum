using System.Collections.Concurrent;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage.InMemory
{
    public class InMemoryBlockStore : IBlockStore
    {
        private readonly object _writeLock = new object();
        private readonly ConcurrentDictionary<string, BlockHeader> _blocksByHash = new();
        private readonly ConcurrentDictionary<BigInteger, string> _hashByNumber = new();
        private long _latestBlockNumber = -1;

        public Task<BlockHeader> GetByHashAsync(byte[] hash)
        {
            var hashHex = ToHex(hash);
            _blocksByHash.TryGetValue(hashHex, out var header);
            return Task.FromResult(header);
        }

        public Task<BlockHeader> GetByNumberAsync(BigInteger number)
        {
            if (!_hashByNumber.TryGetValue(number, out var hashHex))
                return Task.FromResult<BlockHeader>(null);

            _blocksByHash.TryGetValue(hashHex, out var header);
            return Task.FromResult(header);
        }

        public Task<BlockHeader> GetLatestAsync()
        {
            var latest = Interlocked.Read(ref _latestBlockNumber);
            if (latest < 0)
                return Task.FromResult<BlockHeader>(null);

            return GetByNumberAsync(latest);
        }

        public Task<BigInteger> GetHeightAsync()
        {
            return Task.FromResult((BigInteger)Interlocked.Read(ref _latestBlockNumber));
        }

        public Task SaveAsync(BlockHeader header, byte[] blockHash)
        {
            var hashHex = ToHex(blockHash);
            _blocksByHash[hashHex] = header;
            _hashByNumber[header.BlockNumber] = hashHex;

            long blockNum = (long)header.BlockNumber;
            long current;
            do
            {
                current = Interlocked.Read(ref _latestBlockNumber);
                if (blockNum <= current) break;
            } while (Interlocked.CompareExchange(ref _latestBlockNumber, blockNum, current) != current);

            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(byte[] hash)
        {
            var hashHex = ToHex(hash);
            return Task.FromResult(_blocksByHash.ContainsKey(hashHex));
        }

        public Task<byte[]> GetHashByNumberAsync(BigInteger number)
        {
            if (!_hashByNumber.TryGetValue(number, out var hashHex))
                return Task.FromResult<byte[]>(null);

            return Task.FromResult(FromHex(hashHex));
        }

        public Task UpdateBlockHashAsync(BigInteger blockNumber, byte[] newHash)
        {
            lock (_writeLock)
            {
                if (!_hashByNumber.TryGetValue(blockNumber, out var oldHashHex))
                    return Task.CompletedTask;

                var newHashHex = ToHex(newHash);

                if (_blocksByHash.TryRemove(oldHashHex, out var header))
                {
                    _blocksByHash[newHashHex] = header;
                }

                _hashByNumber[blockNumber] = newHashHex;
            }
            return Task.CompletedTask;
        }

        public Task DeleteByNumberAsync(BigInteger blockNumber)
        {
            if (_hashByNumber.TryRemove(blockNumber, out var hashHex))
            {
                _blocksByHash.TryRemove(hashHex, out _);
            }

            long current = Interlocked.Read(ref _latestBlockNumber);
            if ((long)blockNumber == current)
            {
                long newLatest = current - 1;
                while (newLatest >= 0 && !_hashByNumber.ContainsKey(newLatest))
                    newLatest--;
                Interlocked.Exchange(ref _latestBlockNumber, newLatest);
            }

            return Task.CompletedTask;
        }

        public void Clear()
        {
            _blocksByHash.Clear();
            _hashByNumber.Clear();
            Interlocked.Exchange(ref _latestBlockNumber, -1);
        }

        private static string ToHex(byte[] bytes) => bytes?.ToHex();

        private static byte[] FromHex(string hex) => hex?.HexToByteArray();
    }
}
