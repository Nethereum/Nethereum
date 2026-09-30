using Nethereum.DevP2P;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Discv4
{
    public class EnodeUrlTests
    {
        private const string PubKeyHex =
            "abababababababababababababababababababababababababababababababab" +
            "abababababababababababababababababababababababababababababababab";

        [Fact]
        public void Parse_DiscportQueryParameter_SeparatesDiscoveryPortFromTcpPort()
        {
            var enode = $"enode://{PubKeyHex}@10.3.58.6:30303?discport=30301";

            var parsed = EnodeUrl.Parse(enode);

            Assert.Equal("10.3.58.6", parsed.Host);
            Assert.Equal(30303, parsed.Port);
            Assert.Equal(30301, parsed.DiscoveryPort);
            Assert.Equal(64, parsed.PublicKey.Length);
        }

        [Fact]
        public void Parse_NoDiscportQueryParameter_DiscoveryPortEqualsTcpPort()
        {
            var enode = $"enode://{PubKeyHex}@10.3.58.6:30303";

            var parsed = EnodeUrl.Parse(enode);

            Assert.Equal("10.3.58.6", parsed.Host);
            Assert.Equal(30303, parsed.Port);
            Assert.Equal(30303, parsed.DiscoveryPort);
        }

        [Fact]
        public void Parse_TcpPortOutOfUint16Range_Throws()
        {
            var enode = $"enode://{PubKeyHex}@10.3.58.6:70000";

            Assert.Throws<System.ArgumentException>(() => EnodeUrl.Parse(enode));
        }

        [Fact]
        public void Parse_DiscportOutOfUint16Range_ThrowsDistinctFromBadTcpPort()
        {
            var enode = $"enode://{PubKeyHex}@10.3.58.6:30303?discport=70000";

            var ex = Assert.Throws<System.ArgumentException>(() => EnodeUrl.Parse(enode));
            Assert.Contains("discport", ex.Message);
        }

        [Fact]
        public void Parse_GarbageDiscport_Throws()
        {
            var enode = $"enode://{PubKeyHex}@10.3.58.6:30303?discport=notanumber";

            var ex = Assert.Throws<System.ArgumentException>(() => EnodeUrl.Parse(enode));
            Assert.Contains("discport", ex.Message);
        }

        [Fact]
        public void Parse_EmptyDiscportValue_TreatedAsAbsent_NoThrow()
        {
            var enode = $"enode://{PubKeyHex}@10.3.58.6:30303?discport=";

            var parsed = EnodeUrl.Parse(enode);

            Assert.Equal(30303, parsed.Port);
            Assert.Equal(30303, parsed.DiscoveryPort);
        }

        [Fact]
        public void Parse_WrongCaseDiscportKey_Ignored()
        {
            var enode = $"enode://{PubKeyHex}@10.3.58.6:30303?DISCPORT=30301";

            var parsed = EnodeUrl.Parse(enode);

            Assert.Equal(30303, parsed.Port);
            Assert.Equal(30303, parsed.DiscoveryPort);
        }
    }
}
