using System.Collections.Concurrent;
using Nethereum.Util;

namespace Nethereum.DevP2P.Discv5
{
    internal sealed class Discv5SessionStore
    {
        private readonly ConcurrentDictionary<string, Discv5Session> _byKey = new();
        private readonly int _capacity;

        public Discv5SessionStore(int capacity) => _capacity = capacity;

        public int Count => _byKey.Count;

        public bool TryGet(string key, out Discv5Session session) => _byKey.TryGetValue(key, out session);

        public void Store(string key, Discv5Session session)
        {
            EvictOldestIfAtCapacity();
            _byKey[key] = session;
        }

        private void EvictOldestIfAtCapacity()
            => BoundedCacheEviction.EvictOldestByCreatedUtc(_byKey, _capacity,
                value => value.CreatedUtc, key => _byKey.TryRemove(key, out _));
    }
}
