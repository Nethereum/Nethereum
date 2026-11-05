using System.Diagnostics;

namespace Nethereum.AccountAbstraction.Bundler.Execution
{
    internal sealed class BoundedExpiringSet
    {
        private readonly int _capacity;
        private readonly long _ttlTicks;
        private readonly Dictionary<string, (long Expiry, LinkedListNode<string> Node)> _items = new();
        private readonly LinkedList<string> _order = new();
        private readonly object _lock = new();
        private long _generation;

        public BoundedExpiringSet(int capacity, TimeSpan ttl)
        {
            _capacity = capacity;
            _ttlTicks = (long)(ttl.TotalSeconds * Stopwatch.Frequency);
        }

        public bool Contains(string key)
        {
            lock (_lock)
            {
                if (!_items.TryGetValue(key, out var item)) return false;
                if (Stopwatch.GetTimestamp() < item.Expiry) return true;

                RemoveUnlocked(key, item.Node);
                return false;
            }
        }

        public long Generation
        {
            get
            {
                lock (_lock)
                {
                    return _generation;
                }
            }
        }

        public void AddIfUnchanged(string key, long generation)
        {
            if (_capacity <= 0) return;

            lock (_lock)
            {
                if (_generation != generation) return;

                var expiry = Stopwatch.GetTimestamp() + _ttlTicks;
                if (_items.TryGetValue(key, out var existing))
                {
                    _items[key] = (expiry, existing.Node);
                    return;
                }

                _items[key] = (expiry, _order.AddLast(key));

                while (_items.Count > _capacity)
                {
                    RemoveUnlocked(_order.First!.Value, _order.First);
                }
            }
        }

        public void Remove(string key)
        {
            lock (_lock)
            {
                _generation++;
                if (_items.TryGetValue(key, out var item))
                {
                    RemoveUnlocked(key, item.Node);
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _items.Clear();
                _order.Clear();
            }
        }

        private void RemoveUnlocked(string key, LinkedListNode<string> node)
        {
            _items.Remove(key);
            _order.Remove(node);
        }
    }
}
