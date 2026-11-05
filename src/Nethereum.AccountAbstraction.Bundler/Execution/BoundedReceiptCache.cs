using Nethereum.RPC.AccountAbstraction.DTOs;

namespace Nethereum.AccountAbstraction.Bundler.Execution
{
    internal sealed class BoundedReceiptCache
    {
        private readonly int _capacity;
        private readonly Dictionary<string, UserOperationReceipt> _items = new();
        private readonly Queue<string> _order = new();
        private readonly object _lock = new();

        public BoundedReceiptCache(int capacity)
        {
            _capacity = capacity;
        }

        public bool TryGet(string key, out UserOperationReceipt receipt)
        {
            lock (_lock)
            {
                return _items.TryGetValue(key, out receipt!);
            }
        }

        public void Set(string key, UserOperationReceipt receipt)
        {
            if (_capacity <= 0) return;

            lock (_lock)
            {
                if (!_items.ContainsKey(key))
                {
                    _order.Enqueue(key);
                }

                _items[key] = receipt;

                while (_items.Count > _capacity)
                {
                    _items.Remove(_order.Dequeue());
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
    }
}
