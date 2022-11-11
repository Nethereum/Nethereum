using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Network;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Parity.IntegrationTests.Tests.Network
{
    public class ParityNetPeersTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var parityNetPeers = new ParityNetPeers(client);
            return await parityNetPeers.SendRequestAsync().ConfigureAwait(false);
        }

        public override Type GetRequestType()
        {
            return typeof(ParityNetPeers);
        }

        [Fact]
        public async void ShouldNotReturnNull()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
        }
    }
}