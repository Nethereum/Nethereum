using System;
using System.Threading.Tasks;
using Nethereum.Pantheon.RPC.Clique;
using Nethereum.JsonRpc.Client;
using Nethereum.Pantheon.IntegrationTests;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Pantheon.Tests.Testers
{

    public class CliqueGetSignersAtHashTester : RPCRequestTester<string[]>, IRPCRequestTester
    {
        public override async Task<string[]> ExecuteAsync(IClient client)
        {
            var cliqueGetSignersAtHash = new CliqueGetSignersAtHash(client);
            return await cliqueGetSignersAtHash.SendRequestAsync(Settings.GetBlockHash());
        }

        public override Type GetRequestType()
        {
            return typeof(CliqueGetSignersAtHash);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }

}
        