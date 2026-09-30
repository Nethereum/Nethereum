using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace Nethereum.DevP2P.Common
{
    public sealed class TokenBucketRateLimiter<TKey> where TKey : notnull
    {
        private sealed class Bucket
        {
            public double Tokens;
            public long LastRefillTicks;
            public long LastAccessSeq;
            public readonly object Sync = new object();
        }

        private readonly int _rate;
        private readonly int _burst;
        private readonly int _maxCachedKeys;
        private readonly ConcurrentDictionary<TKey, Bucket> _buckets;
        private long _accessCounter;

        public TokenBucketRateLimiter(int rate, int burst, int maxCachedKeys = DevP2PRateLimitConstants.KnownSourcesCacheSize)
        {
            if (rate <= 0) throw new ArgumentOutOfRangeException(nameof(rate), "rate must be > 0");
            if (burst <= 0) throw new ArgumentOutOfRangeException(nameof(burst), "burst must be > 0");
            if (maxCachedKeys <= 0) throw new ArgumentOutOfRangeException(nameof(maxCachedKeys), "maxCachedKeys must be > 0");

            _rate = rate;
            _burst = burst;
            _maxCachedKeys = maxCachedKeys;
            _buckets = new ConcurrentDictionary<TKey, Bucket>();
        }

        public int CachedKeyCount => _buckets.Count;

        public bool TryAcquire(TKey key, int tokens = 1)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (tokens <= 0) throw new ArgumentOutOfRangeException(nameof(tokens), "tokens must be > 0");
            if (tokens > _burst) return false;

            var bucket = _buckets.GetOrAdd(key, CreateBucket);

            if (_buckets.Count > _maxCachedKeys)
            {
                EvictOldest();
            }

            lock (bucket.Sync)
            {
                Refill(bucket);
                bucket.LastAccessSeq = Interlocked.Increment(ref _accessCounter);
                if (bucket.Tokens >= tokens)
                {
                    bucket.Tokens -= tokens;
                    return true;
                }
                return false;
            }
        }

        public void Reset(TKey key)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (!_buckets.TryGetValue(key, out var bucket)) return;
            lock (bucket.Sync)
            {
                bucket.Tokens = _burst;
                bucket.LastRefillTicks = Stopwatch.GetTimestamp();
                bucket.LastAccessSeq = Interlocked.Increment(ref _accessCounter);
            }
        }

        private Bucket CreateBucket(TKey _)
        {
            return new Bucket
            {
                Tokens = _burst,
                LastRefillTicks = Stopwatch.GetTimestamp(),
                LastAccessSeq = Interlocked.Increment(ref _accessCounter),
            };
        }

        private void Refill(Bucket bucket)
        {
            long now = Stopwatch.GetTimestamp();
            long elapsedTicks = now - bucket.LastRefillTicks;
            if (elapsedTicks <= 0) return;
            double elapsedSeconds = elapsedTicks / (double)Stopwatch.Frequency;
            double refilled = elapsedSeconds * _rate;
            if (refilled <= 0) return;
            bucket.Tokens = Math.Min(_burst, bucket.Tokens + refilled);
            bucket.LastRefillTicks = now;
        }

        private void EvictOldest()
        {
            var snapshot = new List<KeyValuePair<TKey, Bucket>>(_buckets);
            int overflow = snapshot.Count - _maxCachedKeys;
            if (overflow <= 0) return;

            snapshot.Sort((a, b) => a.Value.LastAccessSeq.CompareTo(b.Value.LastAccessSeq));
            for (int i = 0; i < overflow && i < snapshot.Count; i++)
            {
                _buckets.TryRemove(snapshot[i].Key, out _);
            }
        }
    }
}
