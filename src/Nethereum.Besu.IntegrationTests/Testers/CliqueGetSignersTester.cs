

using System;
using System.Threading.Tasks;
using Nethereum.Besu.RPC.Clique;
using Nethereum.JsonRpc.Client;
using Nethereum.Besu.IntegrationTests;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Besu.Tests.Testers
{

    public class CliqueGetSignersTester : RPCRequestTester<string[]>, IRPCRequestTester
    {
        public override Task<string[]> ExecuteAsync(IClient client)
        {
            var cliqueGetSigners = new CliqueGetSigners(client);
            return cliqueGetSigners.SendRequestAsync(BlockParameter.CreateLatest());
        }

        public override Type GetRequestType()
        {
            return typeof(CliqueGetSigners);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
        }
    }

}
        