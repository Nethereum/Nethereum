using System;
using System.Collections.Generic;
using Nethereum.Documentation;

namespace Nethereum.CoreChain.Freezer
{
    public sealed class DecodedClusterCache
    {
        private readonly int _maxBlocks;
        private readonly object _lock = new();
        private readonly Dictionary<long, LinkedListNode<Entry>> _map = new();
        private readonly LinkedList<Entry> _recency = new();

        public DecodedClusterCache(int maxBlocks)
        {
            if (maxBlocks <= 0) throw new ArgumentOutOfRangeException(nameof(maxBlocks));
            _maxBlocks = maxBlocks;
        }

        public int Count
        {
            get { lock (_lock) return _map.Count; }
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "DecodedClusterCache.GetOrAdd — decode-once LRU over a block")]
        public DecodedCluster GetOrAdd(long blockNumber, Func<long, DecodedCluster> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            var lazy = PublishOrTouch(blockNumber, factory);
            return lazy.Value;
        }

        public void Invalidate(long blockNumber)
        {
            lock (_lock)
            {
                if (_map.TryGetValue(blockNumber, out var node))
                {
                    _recency.Remove(node);
                    _map.Remove(blockNumber);
                }
            }
        }

        private Lazy<DecodedCluster> PublishOrTouch(long blockNumber, Func<long, DecodedCluster> factory)
        {
            lock (_lock)
            {
                if (_map.TryGetValue(blockNumber, out var existing))
                {
                    _recency.Remove(existing);
                    _recency.AddFirst(existing);
                    return existing.Value.Lazy;
                }

                var entry = new Entry(blockNumber, new Lazy<DecodedCluster>(() => factory(blockNumber)));
                var node = new LinkedListNode<Entry>(entry);
                _recency.AddFirst(node);
                _map[blockNumber] = node;

                EvictLeastRecentlyUsedIfOverBudget();
                return entry.Lazy;
            }
        }

        private void EvictLeastRecentlyUsedIfOverBudget()
        {
            while (_map.Count > _maxBlocks)
            {
                var lru = _recency.Last;
                _recency.RemoveLast();
                _map.Remove(lru.Value.BlockNumber);
            }
        }

        private sealed class Entry
        {
            public long BlockNumber { get; }
            public Lazy<DecodedCluster> Lazy { get; }

            public Entry(long blockNumber, Lazy<DecodedCluster> lazy)
            {
                BlockNumber = blockNumber;
                Lazy = lazy;
            }
        }
    }
}
