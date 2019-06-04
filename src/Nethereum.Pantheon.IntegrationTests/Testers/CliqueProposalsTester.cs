

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

    public class CliqueProposalsTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var cliqueProposals = new CliqueProposals(client);
            return await cliqueProposals.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(CliqueProposals);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }

}
        