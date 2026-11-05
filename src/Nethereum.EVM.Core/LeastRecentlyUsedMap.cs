#if !EVM_ZISK
using System;
using System.Collections.Generic;

namespace Nethereum.EVM
{
    internal sealed class LeastRecentlyUsedMap<TKey, TValue>
    {
        private sealed class Slot
        {
            public TKey Key;
            public TValue Value;
            public long Weight;
        }

        private readonly long _maxWeight;
        private readonly Func<TValue, long> _weigh;
        private long _totalWeight;
        private readonly object _gate = new object();
        private readonly Dictionary<TKey, LinkedListNode<Slot>> _nodesByKey;
        private readonly LinkedList<Slot> _mostRecentFirst = new LinkedList<Slot>();

        public LeastRecentlyUsedMap(long maxWeight, Func<TValue, long> weigh)
        {
            if (maxWeight < 1) throw new ArgumentOutOfRangeException(nameof(maxWeight));
            _maxWeight = maxWeight;
            _weigh = weigh;
            _nodesByKey = new Dictionary<TKey, LinkedListNode<Slot>>();
        }

        public TValue GetOrAdd(TKey key, Func<TValue> build)
        {
            if (TryGetAndMarkRecent(key, out var existing)) return existing;
            return AddUnlessPresent(key, build());
        }

        public void Reweigh(TKey key)
        {
            lock (_gate)
            {
                if (!_nodesByKey.TryGetValue(key, out var node)) return;
                var weight = _weigh(node.Value.Value);
                _totalWeight += weight - node.Value.Weight;
                node.Value.Weight = weight;
                EvictWhileOverWeight();
            }
        }

        private bool TryGetAndMarkRecent(TKey key, out TValue value)
        {
            lock (_gate)
            {
                if (_nodesByKey.TryGetValue(key, out var node))
                {
                    MarkMostRecent(node);
                    value = node.Value.Value;
                    return true;
                }
            }
            value = default;
            return false;
        }

        private TValue AddUnlessPresent(TKey key, TValue built)
        {
            lock (_gate)
            {
                if (_nodesByKey.TryGetValue(key, out var racedIn))
                {
                    MarkMostRecent(racedIn);
                    return racedIn.Value.Value;
                }

                var slot = new Slot { Key = key, Value = built, Weight = _weigh(built) };
                _nodesByKey[key] = _mostRecentFirst.AddFirst(slot);
                _totalWeight += slot.Weight;
                EvictWhileOverWeight();
                return built;
            }
        }

        private void EvictWhileOverWeight()
        {
            while (_totalWeight > _maxWeight && _nodesByKey.Count > 1) EvictLeastRecent();
        }

        private void MarkMostRecent(LinkedListNode<Slot> node)
        {
            _mostRecentFirst.Remove(node);
            _mostRecentFirst.AddFirst(node);
        }

        private void EvictLeastRecent()
        {
            var leastRecent = _mostRecentFirst.Last;
            _mostRecentFirst.RemoveLast();
            _nodesByKey.Remove(leastRecent.Value.Key);
            _totalWeight -= leastRecent.Value.Weight;
        }
    }
}
#endif
