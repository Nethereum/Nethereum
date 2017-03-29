
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;
using Newtonsoft.Json.Linq;
using Nethereum.Parity.RPC.Network;

namespace Nethereum.Parity.Test.Testers
{
    public class ParityChainStatusTester : RPCRequestTester<JObject>, IRPCRequestTester
    {

        [Fact]
        public async void ShouldNotReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var parityChainStatus = new ParityChainStatus(client);
            return await parityChainStatus.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityChainStatus);
        }
    }
}
        