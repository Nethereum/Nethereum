using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.CoreChain.Engine
{
    public sealed class PayloadBuildRegistry : IPayloadBuildRegistry
    {
        private sealed class Entry
        {
            public Entry(Task<EnginePayloadBuild> build)
            {
                Build = build;
                RegisteredAt = DateTimeOffset.UtcNow;
            }

            public Task<EnginePayloadBuild> Build { get; }

            public DateTimeOffset RegisteredAt { get; }
        }

        private readonly ConcurrentDictionary<string, Entry> _builds = new();
        private readonly TimeSpan _ttl;

        public PayloadBuildRegistry(TimeSpan? ttl = null)
        {
            _ttl = ttl ?? TimeSpan.FromMinutes(2);
        }

        public string Register(Task<EnginePayloadBuild> build)
        {
            if (build == null) throw new ArgumentNullException(nameof(build));

            EvictExpired();

            var payloadId = NewPayloadId();
            _builds[payloadId] = new Entry(build);
            return payloadId;
        }

        public Task<EnginePayloadBuild> Get(string payloadId)
        {
            if (payloadId != null && _builds.TryGetValue(payloadId, out var entry))
                return entry.Build;

            throw new UnknownPayloadException(payloadId);
        }

        private void EvictExpired()
        {
            var cutoff = DateTimeOffset.UtcNow - _ttl;
            foreach (var kvp in _builds)
            {
                if (kvp.Value.RegisteredAt < cutoff)
                    _builds.TryRemove(kvp.Key, out _);
            }
        }

        private static string NewPayloadId()
        {
            var bytes = new byte[8];
            RandomNumberGenerator.Fill(bytes);
            return bytes.ToHex(true);
        }

        public sealed class UnknownPayloadException : Exception
        {
            public UnknownPayloadException(string payloadId)
                : base($"Unknown or expired payload id: {payloadId}")
            {
            }
        }
    }
}
