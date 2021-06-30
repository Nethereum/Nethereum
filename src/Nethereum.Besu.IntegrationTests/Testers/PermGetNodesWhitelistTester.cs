

using System;
using System.Threading.Tasks;
using Nethereum.Besu.RPC.Permissioning;
using Nethereum.JsonRpc.Client;
using Nethereum.Besu.IntegrationTests;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Besu.Tests.Testers
{

    public class PermGetNodesWhitelistTester : RPCRequestTester<string[]>, IRPCRequestTester
    {
        public override async Task<string[]> ExecuteAsync(IClient client)
        {
            var permGetNodesWhitelist = new PermGetNodesWhitelist(client);
            return await permGetNodesWhitelist.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(PermGetNodesWhitelist);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }

}
        