

using System;
using System.Threading.Tasks;
using Nethereum.Pantheon.RPC.Clique;
using Nethereum.JsonRpc.Client;
using Nethereum.Pantheon.IntegrationTests;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Pantheon.Tests.Testers
{

    public class CliqueGetSignersTester : RPCRequestTester<string[]>, IRPCRequestTester
    {
        public override async Task<string[]> ExecuteAsync(IClient client)
        {
            var cliqueGetSigners = new CliqueGetSigners(client);
            return await cliqueGetSigners.SendRequestAsync(BlockParameter.CreateLatest());
        }

        public override Type GetRequestType()
        {
            return typeof(CliqueGetSigners);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }

}
        