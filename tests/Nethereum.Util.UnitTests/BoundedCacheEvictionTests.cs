using System;
using System.Collections.Concurrent;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class BoundedCacheEvictionTests
    {
        private sealed class Entry
        {
            public DateTime CreatedUtc { get; init; }
        }

        [Fact]
        public void Given_MapAtCapacity_When_EvictOldest_Then_OldestByCreatedUtcRemoved()
        {
            var map = new ConcurrentDictionary<string, Entry>();
            var baseTime = DateTime.UtcNow.AddHours(-1);
            const int capacity = 8;
            for (int i = 0; i < capacity; i++)
                map[$"key{i}"] = new Entry { CreatedUtc = baseTime.AddSeconds(i) };

            BoundedCacheEviction.EvictOldestByCreatedUtc(
                map, capacity, value => value.CreatedUtc, key => map.TryRemove(key, out _));

            Assert.False(map.ContainsKey("key0"), "the oldest entry should have been evicted");
            Assert.Equal(capacity - 1, map.Count);
        }

        [Fact]
        public void Given_MapBelowCapacity_When_EvictOldest_Then_NothingRemoved()
        {
            var map = new ConcurrentDictionary<string, Entry>();
            var baseTime = DateTime.UtcNow.AddHours(-1);
            for (int i = 0; i < 3; i++)
                map[$"key{i}"] = new Entry { CreatedUtc = baseTime.AddSeconds(i) };

            BoundedCacheEviction.EvictOldestByCreatedUtc(
                map, capacity: 8, value => value.CreatedUtc, key => map.TryRemove(key, out _));

            Assert.Equal(3, map.Count);
        }
    }
}
