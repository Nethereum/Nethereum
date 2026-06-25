using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Nethereum.DevP2P.Netutil;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Netutil
{
    /// <summary>
    /// Spec-level tests for <see cref="SubnetTracker"/>. Mirrors geth's
    /// <c>p2p/discover/distinct_net_test.go</c> coverage of bucket/table
    /// subnet diversity — admission caps per /24 IPv4 (and /64 IPv6) prevent
    /// a single subnet owner from filling the pool.
    /// </summary>
    public class SubnetTrackerTests
    {
        [Fact]
        public void Given_BelowLimit_When_TryAddSameSubnet_Then_AllSucceed()
        {
            var tracker = new SubnetTracker(maxPerIPv4Subnet: 10, ipv4Prefix: 24);

            for (int i = 1; i <= 9; i++)
            {
                var ip = IPAddress.Parse($"203.0.113.{i}");
                Assert.True(tracker.TryAdd(ip), $"admission #{i} should succeed");
            }

            Assert.Equal(9, tracker.Count(IPAddress.Parse("203.0.113.42")));
        }

        [Fact]
        public void Given_AtLimit_When_TryAddSameSubnet_Then_OverflowRejected()
        {
            var tracker = new SubnetTracker(maxPerIPv4Subnet: 10, ipv4Prefix: 24);

            for (int i = 1; i <= 10; i++)
                Assert.True(tracker.TryAdd(IPAddress.Parse($"203.0.113.{i}")));

            Assert.False(tracker.TryAdd(IPAddress.Parse("203.0.113.250")),
                "11th admission in the same /24 must be rejected");
            Assert.Equal(10, tracker.Count(IPAddress.Parse("203.0.113.1")));
        }

        [Fact]
        public void Given_DifferentSubnets_When_FilledIndependently_Then_AllAdmitted()
        {
            var tracker = new SubnetTracker(maxPerIPv4Subnet: 10, ipv4Prefix: 24);

            for (int i = 1; i <= 10; i++)
                Assert.True(tracker.TryAdd(IPAddress.Parse($"203.0.113.{i}")));
            for (int i = 1; i <= 10; i++)
                Assert.True(tracker.TryAdd(IPAddress.Parse($"198.51.100.{i}")),
                    $"admission #{i} in second /24 should succeed");

            Assert.Equal(10, tracker.Count(IPAddress.Parse("203.0.113.99")));
            Assert.Equal(10, tracker.Count(IPAddress.Parse("198.51.100.99")));
        }

        [Fact]
        public void Given_AtLimit_When_OneRemoved_Then_NextAdmissionSucceeds()
        {
            var tracker = new SubnetTracker(maxPerIPv4Subnet: 3, ipv4Prefix: 24);
            var ip1 = IPAddress.Parse("203.0.113.1");
            var ip2 = IPAddress.Parse("203.0.113.2");
            var ip3 = IPAddress.Parse("203.0.113.3");
            var ip4 = IPAddress.Parse("203.0.113.4");

            Assert.True(tracker.TryAdd(ip1));
            Assert.True(tracker.TryAdd(ip2));
            Assert.True(tracker.TryAdd(ip3));
            Assert.False(tracker.TryAdd(ip4));

            tracker.Remove(ip2);
            Assert.True(tracker.TryAdd(ip4), "freed slot should accept next admission");
            Assert.Equal(3, tracker.Count(ip1));
        }

        [Fact]
        public void Given_IPv6Address_When_Tracked_Then_GroupedBySixtyFourPrefix()
        {
            var tracker = new SubnetTracker(
                maxPerIPv4Subnet: 10, ipv4Prefix: 24,
                maxPerIPv6Subnet: 2, ipv6Prefix: 64);

            // Same /64 prefix, different interface IDs — counted as one subnet.
            Assert.True(tracker.TryAdd(IPAddress.Parse("2001:db8:1234:5678::1")));
            Assert.True(tracker.TryAdd(IPAddress.Parse("2001:db8:1234:5678::2")));
            Assert.False(tracker.TryAdd(IPAddress.Parse("2001:db8:1234:5678::3")),
                "third /64-member must be rejected");

            // Different /64 prefix — independent slot.
            Assert.True(tracker.TryAdd(IPAddress.Parse("2001:db8:1234:9999::1")),
                "different /64 should be tracked independently");

            // v4 quota unaffected by v6 traffic.
            for (int i = 1; i <= 10; i++)
                Assert.True(tracker.TryAdd(IPAddress.Parse($"203.0.113.{i}")));
        }

        [Fact]
        public async Task Given_ConcurrentAdds_When_FillingSameSubnet_Then_NoOverCount()
        {
            const int maxPerSubnet = 10;
            const int contenders = 200;
            var tracker = new SubnetTracker(maxPerIPv4Subnet: maxPerSubnet, ipv4Prefix: 24);

            int admitted = 0;
            var tasks = Enumerable.Range(1, contenders).Select(i => Task.Run(() =>
            {
                var ip = IPAddress.Parse($"203.0.113.{(i % 254) + 1}");
                if (tracker.TryAdd(ip))
                    System.Threading.Interlocked.Increment(ref admitted);
            })).ToArray();

            await Task.WhenAll(tasks);

            Assert.Equal(maxPerSubnet, admitted);
            Assert.Equal(maxPerSubnet, tracker.Count(IPAddress.Parse("203.0.113.1")));
        }

        [Fact]
        public void Given_LoopbackAddress_When_Checked_Then_AlwaysAdmittedAndUntracked()
        {
            var tracker = new SubnetTracker(maxPerIPv4Subnet: 1, ipv4Prefix: 24);

            for (int i = 0; i < 50; i++)
                Assert.True(tracker.TryAdd(IPAddress.Loopback),
                    "loopback must never exhaust subnet quota");

            // The cap can still apply to a real address — loopback admissions
            // never landed in the same bucket.
            Assert.True(tracker.TryAdd(IPAddress.Parse("203.0.113.1")));
            Assert.False(tracker.TryAdd(IPAddress.Parse("203.0.113.2")));
        }

        [Fact]
        public void Given_FamilyDisabled_When_TryAdd_Then_AdmissionUnconditional()
        {
            var tracker = new SubnetTracker(
                maxPerIPv4Subnet: 0,
                maxPerIPv6Subnet: 0);

            for (int i = 0; i < 1000; i++)
                Assert.True(tracker.TryAdd(IPAddress.Parse($"203.0.113.{i % 254}")));
            for (int i = 0; i < 1000; i++)
                Assert.True(tracker.TryAdd(IPAddress.Parse($"2001:db8::{i:x}")));
        }
    }
}
