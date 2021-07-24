

using System;
using System.Threading.Tasks;
using Nethereum.Besu.RPC.Clique;
using Nethereum.JsonRpc.Client;
using Nethereum.Besu.IntegrationTests;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Besu.Tests.Testers
{

    public class CliqueProposalsTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        public override Task<JObject> ExecuteAsync(IClient client)
        {
            var cliqueProposals = new CliqueProposals(client);
            return cliqueProposals.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(CliqueProposals);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
        }
    }

}
        