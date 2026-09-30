using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Nethereum.DevP2P.Netutil
{
    public static class ConcurrentCounter
    {
        public static bool TryReserve<TKey>(ConcurrentDictionary<TKey, int> counts, TKey key, int cap)
            where TKey : notnull
        {
            if (cap <= 0) return false;
            while (true)
            {
                if (counts.TryGetValue(key, out var n))
                {
                    if (n >= cap) return false;
                    if (counts.TryUpdate(key, n + 1, n)) return true;
                }
                else if (counts.TryAdd(key, 1))
                {
                    return true;
                }
            }
        }

        public static void DecrementOrRemove<TKey>(ConcurrentDictionary<TKey, int> counts, TKey key)
            where TKey : notnull
        {
            while (counts.TryGetValue(key, out var n))
            {
                if (n <= 1)
                {
                    var kvp = new KeyValuePair<TKey, int>(key, n);
                    if (((ICollection<KeyValuePair<TKey, int>>)counts).Remove(kvp))
                        return;
                }
                else if (counts.TryUpdate(key, n - 1, n))
                {
                    return;
                }
            }
        }
    }
}
