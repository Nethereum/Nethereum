using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace Nethereum.DevP2P.Common
{
    /// <summary>
    /// Per-key token bucket rate limiter with LRU eviction across keys.
    /// Each key (typically an <see cref="System.Net.IPAddress"/>, peer node-id,
    /// or remote endpoint) gets an independent bucket that refills continuously
    /// at <c>rate</c> tokens/second up to <c>burst</c> capacity. <see cref="TryAcquire"/>
    /// consumes tokens and returns whether the call is admitted.
    /// <para>
    /// The bucket-cache cap defaults to
    /// <see cref="DevP2PRateLimitConstants.KnownSourcesCacheSize"/>; on overflow the
    /// least-recently-acquired key is evicted. This mirrors the policy in sigp/discv5
    /// <c>socket/filter/mod.rs</c> (LRU of seen sources keyed by IP). Rate-bucket
    /// rate-limiting at the inbound discovery boundary is itself a sigp/discv5 design
    /// choice — the discv5 wire spec does not mandate per-IP rate limits.
    /// </para>
    /// <para>
    /// Thread-safety: <see cref="TryAcquire"/> on different keys is fully concurrent;
    /// on the same key the bucket's tokens-and-last-refill state is updated under a
    /// per-bucket lock so multiple racing acquires are linearised.
    /// </para>
    /// </summary>
    /// <typeparam name="TKey">Bucket identity (IP, node-id, endpoint).</typeparam>
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

        /// <summary>
        /// Construct a rate limiter.
        /// </summary>
        /// <param name="rate">Refill rate in tokens per second. Must be &gt; 0.</param>
        /// <param name="burst">Bucket capacity. Must be &gt; 0. A fresh bucket starts full.</param>
        /// <param name="maxCachedKeys">
        /// Maximum number of distinct keys held in memory before LRU eviction kicks in.
        /// Defaults to <see cref="DevP2PRateLimitConstants.KnownSourcesCacheSize"/>
        /// (sigp/discv5 <c>KNOWN_ADDRS_SIZE</c>).
        /// </param>
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

        /// <summary>
        /// Number of distinct keys currently held in the cache. Includes idle buckets
        /// that have not been evicted yet.
        /// </summary>
        public int CachedKeyCount => _buckets.Count;

        /// <summary>
        /// Attempt to consume <paramref name="tokens"/> tokens from the bucket for
        /// <paramref name="key"/>. Returns <c>true</c> when admitted; <c>false</c>
        /// when the bucket lacks tokens. A fresh key's bucket starts full at
        /// <c>burst</c> capacity.
        /// </summary>
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

        /// <summary>
        /// Reset the bucket for <paramref name="key"/> to a fresh full state. If the
        /// key has no bucket this call is a no-op.
        /// </summary>
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
