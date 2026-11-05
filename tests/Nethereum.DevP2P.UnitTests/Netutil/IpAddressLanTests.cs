using System.Net;
using Nethereum.DevP2P.Netutil;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Netutil
{
    public class IpAddressLanTests
    {
        [Theory]
        [InlineData("127.0.0.1", true)]
        [InlineData("127.255.255.254", true)]
        [InlineData("::1", true)]
        [InlineData("10.0.0.1", true)]
        [InlineData("10.255.255.255", true)]
        [InlineData("172.16.0.1", true)]
        [InlineData("172.31.255.255", true)]
        [InlineData("172.15.255.255", false)]
        [InlineData("172.32.0.1", false)]
        [InlineData("192.168.0.1", true)]
        [InlineData("192.169.0.1", false)]
        [InlineData("169.254.1.1", true)]
        [InlineData("169.253.1.1", false)]
        [InlineData("fc00::1", true)]
        [InlineData("fdff::1", true)]
        [InlineData("fe00::1", false)]
        [InlineData("fe80::1", true)]
        [InlineData("febf::1", true)]
        [InlineData("fec0::1", false)]
        [InlineData("::ffff:127.0.0.1", true)]
        [InlineData("::ffff:10.1.2.3", true)]
        [InlineData("::ffff:8.8.8.8", false)]
        [InlineData("8.8.8.8", false)]
        [InlineData("1.2.3.4", false)]
        [InlineData("100.64.0.1", false)]
        [InlineData("0.0.0.0", false)]
        [InlineData("2001:db8::1", false)]
        [InlineData("2606:4700::1111", false)]
        public void Given_Address_When_ClassifiedAsLan_Then_MatchesGethAddrIsLAN(string address, bool expected)
        {
            Assert.Equal(expected, IPAddress.Parse(address).IsLan());
        }
    }
}
