using System;
using System.Collections.Concurrent;

namespace Nethereum.Util
{
    public static class BoundedCacheEviction
    {
        public static void EvictOldestByCreatedUtc<TValue>(
            ConcurrentDictionary<string, TValue> map,
            int capacity,
            Func<TValue, DateTime> createdUtcSelector,
            Action<string> removeByKey)
        {
            if (map.Count < capacity) return;

            string oldestKey = null;
            var oldestCreatedUtc = DateTime.MaxValue;
            foreach (var entry in map)
            {
                var createdUtc = createdUtcSelector(entry.Value);
                if (createdUtc < oldestCreatedUtc)
                {
                    oldestCreatedUtc = createdUtc;
                    oldestKey = entry.Key;
                }
            }

            if (oldestKey != null) removeByKey(oldestKey);
        }
    }
}
