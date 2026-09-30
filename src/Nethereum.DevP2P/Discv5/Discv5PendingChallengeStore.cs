using System;
using System.Collections.Concurrent;
using Nethereum.Util;

namespace Nethereum.DevP2P.Discv5
{
    internal sealed class Discv5PendingChallengeStore
    {
        private readonly ConcurrentDictionary<string, Discv5PendingChallenge> _byKey = new();
        private readonly int _capacity;

        public Discv5PendingChallengeStore(int capacity) => _capacity = capacity;

        public int Count => _byKey.Count;

        public Discv5PendingChallenge GetOrNull(string key)
            => _byKey.TryGetValue(key, out var challenge) ? challenge : null;

        public bool Consume(string key, out Discv5PendingChallenge challenge)
            => _byKey.TryRemove(key, out challenge);

        public void Add(string key, Discv5PendingChallenge challenge)
        {
            EvictOldestIfAtCapacity();
            _byKey[key] = challenge;
        }

        public void SweepOlderThan(DateTime now, TimeSpan ttl)
        {
            foreach (var kvp in _byKey)
            {
                if (now - kvp.Value.CreatedUtc > ttl)
                {
                    _byKey.TryRemove(kvp.Key, out _);
                }
            }
        }

        private void EvictOldestIfAtCapacity()
            => BoundedCacheEviction.EvictOldestByCreatedUtc(_byKey, _capacity,
                value => value.CreatedUtc, key => _byKey.TryRemove(key, out _));
    }
}
