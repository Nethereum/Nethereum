using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.DevP2P.Discv5
{
    internal sealed class Discv5PendingOutbound
    {
        public byte[] MessagePlaintext;
        public byte[] PeerStaticPubKey;
        public byte[] LocalEnrEncoded;
        public byte[] InitialNonce;
        public string SessionKey;
        public byte[] RemoteNodeId;
        public IPEndPoint RemoteAddr;
        public DateTime CreatedUtc;
    }

    internal sealed class Discv5PendingOutboundStore
    {
        private readonly ConcurrentDictionary<string, Discv5PendingOutbound> _bySessionKey = new();
        private readonly ConcurrentDictionary<string, Discv5PendingOutbound> _byNonce = new();
        private readonly object _mutationLock = new object();
        private readonly int _capacity;

        public Discv5PendingOutboundStore(int capacity) => _capacity = capacity;

        public int Count => _bySessionKey.Count;

        public int NonceIndexCount => _byNonce.Count;

        public void Add(Discv5PendingOutbound pending)
        {
            lock (_mutationLock)
            {
                EvictOldestIfAtCapacity();
                if (_bySessionKey.TryGetValue(pending.SessionKey, out var existing) && existing.InitialNonce != null)
                {
                    _byNonce.TryRemove(existing.InitialNonce.ToHex(), out _);
                }
                _bySessionKey[pending.SessionKey] = pending;
                _byNonce[pending.InitialNonce.ToHex()] = pending;
            }
        }

        public bool TryGetByNonce(string nonceHex, out Discv5PendingOutbound pending)
            => _byNonce.TryGetValue(nonceHex, out pending);

        public void Remove(string sessionKey)
        {
            if (sessionKey == null) return;
            lock (_mutationLock)
            {
                if (_bySessionKey.TryRemove(sessionKey, out var removed) && removed?.InitialNonce != null)
                {
                    _byNonce.TryRemove(removed.InitialNonce.ToHex(), out _);
                }
            }
        }

        public void SweepOlderThan(DateTime now, TimeSpan ttl)
        {
            foreach (var kvp in _bySessionKey)
            {
                if (now - kvp.Value.CreatedUtc > ttl)
                {
                    Remove(kvp.Key);
                }
            }
        }

        public IReadOnlyList<Discv5PendingOutbound> Snapshot()
            => new List<Discv5PendingOutbound>(_bySessionKey.Values);

        private void EvictOldestIfAtCapacity()
            => BoundedCacheEviction.EvictOldestByCreatedUtc(_bySessionKey, _capacity,
                value => value.CreatedUtc, Remove);
    }
}
