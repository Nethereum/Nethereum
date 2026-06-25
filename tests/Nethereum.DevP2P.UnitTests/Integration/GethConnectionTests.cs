using System;
using System.Threading.Tasks;
using Nethereum.DevP2P.Rlpx;
using Xunit;

namespace Nethereum.DevP2P.UnitTests.Integration
{
    [Trait("Category", "Integration")]
    public class GethConnectionTests
    {
        private string GetEnode() =>
            Environment.GetEnvironmentVariable("GETH_ENODE")
            ?? throw new SkipException("Set GETH_ENODE to run integration tests");

        [Fact]
        public async Task ConnectToGeth_ExchangeHello()
        {
            var enode = GetEnode();
            var connector = new StaticPeerConnector();
            var conn = await connector.ConnectAsync(enode);

            Assert.True(conn.IsConnected);
            Assert.NotNull(conn.RemoteHello);
            Assert.True(conn.RemoteHello.ProtocolVersion >= 5);
            Assert.NotEmpty(conn.RemoteHello.ClientId);
            Assert.NotEmpty(conn.SharedCapabilities);

            await conn.DisconnectAsync();
        }

        [Fact]
        public async Task ConnectToGeth_SharedEth68()
        {
            var enode = GetEnode();
            var connector = new StaticPeerConnector();
            var conn = await connector.ConnectAsync(enode);

            var ethCap = conn.SharedCapabilities.Find(c => c.Name == "eth");
            Assert.NotNull(ethCap);
            Assert.True(ethCap.Version >= 68);

            await conn.DisconnectAsync();
        }
    }

    public class SkipException : Exception
    {
        public SkipException(string message) : base(message) { }
    }
}
