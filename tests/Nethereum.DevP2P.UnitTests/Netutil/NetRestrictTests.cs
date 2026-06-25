using System;
using System.Net;
using Nethereum.DevP2P.Netutil;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Netutil
{
    /// <summary>
    /// Spec-level tests for <see cref="NetRestrict"/>. Mirrors geth's
    /// <c>p2p/netutil/net_test.go</c> coverage of <c>Netlist.Contains</c>
    /// — empty list = no restriction, v4 and v6 CIDR both supported, mixed
    /// lists work, malformed input throws, IPv4-mapped-IPv6 addresses
    /// classify against v4 entries.
    /// </summary>
    public class NetRestrictTests
    {
        [Fact]
        public void Given_EmptyList_When_AnyIpChecked_Then_Allowed()
        {
            var nr = new NetRestrict();

            Assert.Equal(0, nr.Count);
            Assert.True(nr.Contains(IPAddress.Parse("1.2.3.4")));
            Assert.True(nr.Contains(IPAddress.Parse("10.0.0.1")));
            Assert.True(nr.Contains(IPAddress.Parse("::1")));
            Assert.True(nr.Contains(IPAddress.Parse("2001:db8::1")));
        }

        [Fact]
        public void Given_IPv4Cidr_When_IpInsideOrOutside_Then_ClassifiedCorrectly()
        {
            var nr = new NetRestrict();
            nr.Add("10.0.0.0/8");

            Assert.Equal(1, nr.Count);
            Assert.True(nr.Contains(IPAddress.Parse("10.0.0.1")));
            Assert.True(nr.Contains(IPAddress.Parse("10.255.255.254")));
            Assert.False(nr.Contains(IPAddress.Parse("11.0.0.1")));
            Assert.False(nr.Contains(IPAddress.Parse("192.168.0.1")));
        }

        [Fact]
        public void Given_IPv4Slash24_When_IpInsideOrOutside_Then_ClassifiedCorrectly()
        {
            var nr = new NetRestrict();
            nr.Add("192.168.1.0/24");

            Assert.True(nr.Contains(IPAddress.Parse("192.168.1.0")));
            Assert.True(nr.Contains(IPAddress.Parse("192.168.1.42")));
            Assert.True(nr.Contains(IPAddress.Parse("192.168.1.255")));
            Assert.False(nr.Contains(IPAddress.Parse("192.168.2.1")));
            Assert.False(nr.Contains(IPAddress.Parse("192.168.0.255")));
        }

        [Fact]
        public void Given_IPv6Cidr_When_IpInsideOrOutside_Then_ClassifiedCorrectly()
        {
            var nr = new NetRestrict();
            nr.Add("2001:db8::/32");

            Assert.True(nr.Contains(IPAddress.Parse("2001:db8::1")));
            Assert.True(nr.Contains(IPAddress.Parse("2001:db8:ffff::abcd")));
            Assert.False(nr.Contains(IPAddress.Parse("2001:db9::1")));
            Assert.False(nr.Contains(IPAddress.Parse("::1")));
        }

        [Fact]
        public void Given_MixedV4AndV6Cidrs_When_Checked_Then_BothFamiliesMatch()
        {
            var nr = new NetRestrict();
            nr.Add("10.0.0.0/8");
            nr.Add("2001:db8::/32");

            Assert.Equal(2, nr.Count);
            Assert.True(nr.Contains(IPAddress.Parse("10.5.5.5")));
            Assert.True(nr.Contains(IPAddress.Parse("2001:db8::42")));
            Assert.False(nr.Contains(IPAddress.Parse("11.0.0.1")));
            Assert.False(nr.Contains(IPAddress.Parse("2001:db9::1")));
        }

        [Fact]
        public void Given_StandardPrivateRanges_When_CommonLanIpsChecked_Then_AllAccepted()
        {
            var nr = new NetRestrict();
            nr.Add("10.0.0.0/8");
            nr.Add("172.16.0.0/12");
            nr.Add("192.168.0.0/16");

            Assert.True(nr.Contains(IPAddress.Parse("10.1.2.3")));
            Assert.True(nr.Contains(IPAddress.Parse("172.16.0.1")));
            Assert.True(nr.Contains(IPAddress.Parse("172.31.255.254")));
            Assert.True(nr.Contains(IPAddress.Parse("192.168.0.1")));
            Assert.True(nr.Contains(IPAddress.Parse("192.168.255.254")));

            Assert.False(nr.Contains(IPAddress.Parse("8.8.8.8")));
            Assert.False(nr.Contains(IPAddress.Parse("172.32.0.1")));
            Assert.False(nr.Contains(IPAddress.Parse("172.15.255.254")));
            Assert.False(nr.Contains(IPAddress.Parse("193.168.0.1")));
        }

        [Fact]
        public void Given_BareIpv4_When_Added_Then_TreatedAsSlash32()
        {
            var nr = new NetRestrict();
            nr.Add("10.0.0.5");

            Assert.True(nr.Contains(IPAddress.Parse("10.0.0.5")));
            Assert.False(nr.Contains(IPAddress.Parse("10.0.0.6")));
            Assert.False(nr.Contains(IPAddress.Parse("10.0.0.4")));
        }

        [Fact]
        public void Given_BareIpv6_When_Added_Then_TreatedAsSlash128()
        {
            var nr = new NetRestrict();
            nr.Add("2001:db8::1");

            Assert.True(nr.Contains(IPAddress.Parse("2001:db8::1")));
            Assert.False(nr.Contains(IPAddress.Parse("2001:db8::2")));
        }

        [Fact]
        public void Given_MalformedCidr_When_Added_Then_ArgumentException()
        {
            var nr = new NetRestrict();

            Assert.Throws<ArgumentException>(() => nr.Add(null));
            Assert.Throws<ArgumentException>(() => nr.Add(""));
            Assert.Throws<ArgumentException>(() => nr.Add("   "));
            Assert.Throws<ArgumentException>(() => nr.Add("not-a-cidr"));
            Assert.Throws<ArgumentException>(() => nr.Add("10.0.0.0/abc"));
            Assert.Throws<ArgumentException>(() => nr.Add("10.0.0.0/33"));
            Assert.Throws<ArgumentException>(() => nr.Add("999.0.0.0/8"));
        }

        [Fact]
        public void Given_NonEmptyList_When_NullIpChecked_Then_Rejected()
        {
            var nr = new NetRestrict();
            nr.Add("10.0.0.0/8");

            Assert.False(nr.Contains(null));
        }

        [Fact]
        public void Given_Ipv4Cidr_When_IPv4MappedIpv6Checked_Then_UnwrapsAndMatches()
        {
            var nr = new NetRestrict();
            nr.Add("10.0.0.0/8");

            var mapped = IPAddress.Parse("10.5.5.5").MapToIPv6();
            Assert.True(nr.Contains(mapped));

            var mappedOutside = IPAddress.Parse("11.0.0.1").MapToIPv6();
            Assert.False(nr.Contains(mappedOutside));
        }

        [Fact]
        public void Given_Cidr_When_WhitespacePadded_Then_Accepted()
        {
            var nr = new NetRestrict();
            nr.Add("  10.0.0.0/8  ");

            Assert.True(nr.Contains(IPAddress.Parse("10.5.5.5")));
        }
    }
}
