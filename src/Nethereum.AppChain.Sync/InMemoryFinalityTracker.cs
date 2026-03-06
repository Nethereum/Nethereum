using System.Collections.Concurrent;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.AppChain.Sync
{
    public class InMemoryFinalityTracker : IFinalityTracker
    {
        private readonly ConcurrentDictionary<BigInteger, BlockFinality> _overrides = new();
        private BigInteger _lastFinalizedBlock = -1;
        private BigInteger _lastSoftBlock = -1;
        private readonly object _lock = new();

        public BigInteger LastFinalizedBlock => _lastFinalizedBlock;
        public BigInteger LastSoftBlock => _lastSoftBlock;

        public Task<bool> IsFinalizedAsync(BigInteger blockNumber)
        {
            if (blockNumber <= _lastFinalizedBlock)
                return Task.FromResult(true);

            if (_overrides.TryGetValue(blockNumber, out var finality))
                return Task.FromResult(finality == BlockFinality.Finalized);

            return Task.FromResult(false);
        }

        public Task<bool> IsSoftAsync(BigInteger blockNumber)
        {
            if (blockNumber <= _lastFinalizedBlock)
                return Task.FromResult(false);

            if (_overrides.TryGetValue(blockNumber, out var finality))
                return Task.FromResult(finality == BlockFinality.Soft);

            return Task.FromResult(blockNumber > _lastFinalizedBlock && blockNumber <= _lastSoftBlock);
        }

        public Task MarkAsFinalizedAsync(BigInteger blockNumber)
        {
            lock (_lock)
            {
                if (blockNumber > _lastFinalizedBlock)
                {
                    _lastFinalizedBlock = blockNumber;
                    PruneOverrides();
                }
            }
            return Task.CompletedTask;
        }

        public Task MarkAsSoftAsync(BigInteger blockNumber)
        {
            if (blockNumber > _lastFinalizedBlock)
            {
                _overrides[blockNumber] = BlockFinality.Soft;
            }
            lock (_lock)
            {
                if (blockNumber > _lastSoftBlock)
                {
                    _lastSoftBlock = blockNumber;
                }
            }
            return Task.CompletedTask;
        }

        public Task MarkRangeAsFinalizedAsync(BigInteger fromBlock, BigInteger toBlock)
        {
            lock (_lock)
            {
                if (toBlock > _lastFinalizedBlock)
                {
                    _lastFinalizedBlock = toBlock;
                    PruneOverrides();
                }
            }
            return Task.CompletedTask;
        }

        public Task<BigInteger> GetLatestFinalizedBlockAsync()
        {
            return Task.FromResult(_lastFinalizedBlock);
        }

        public Task<BigInteger> GetLatestSoftBlockAsync()
        {
            return Task.FromResult(_lastSoftBlock);
        }

        private void PruneOverrides()
        {
            foreach (var key in _overrides.Keys)
            {
                if (key <= _lastFinalizedBlock)
                {
                    _overrides.TryRemove(key, out _);
                }
            }
        }
    }
}
