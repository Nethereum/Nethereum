using System.Collections.Generic;
using System.Net;

namespace Nethereum.DevP2P.Netutil
{
    internal sealed class RoutingIpLimits
    {
        public const int BucketLimit = 2;
        public const int TableLimit = 10;
        public const int SubnetBits = 24;

        private readonly SubnetTracker _table = New(TableLimit);
        private readonly Dictionary<int, SubnetTracker> _buckets = new Dictionary<int, SubnetTracker>();

        public bool TryAdd(int bucket, IPAddress ip)
        {
            if (ip == null) return false;
            if (ip.IsLan()) return true;
            if (!_table.TryAdd(ip)) return false;
            if (!_buckets.TryGetValue(bucket, out var tracker))
            {
                tracker = New(BucketLimit);
                _buckets[bucket] = tracker;
            }
            if (tracker.TryAdd(ip)) return true;
            _table.Remove(ip);
            return false;
        }

        public void Remove(int bucket, IPAddress ip)
        {
            if (ip == null || ip.IsLan()) return;
            _table.Remove(ip);
            if (_buckets.TryGetValue(bucket, out var tracker)) tracker.Remove(ip);
        }

        private static SubnetTracker New(int limit) => new SubnetTracker(limit, SubnetBits, limit, SubnetBits);
    }
}
