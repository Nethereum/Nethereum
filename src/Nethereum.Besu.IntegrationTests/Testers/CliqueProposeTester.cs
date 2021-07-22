

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

    public class CliqueProposeTester : RPCRequestTester<bool>, IRPCRequestTester
    {
        public override Task<bool> ExecuteAsync(IClient client)
        {
            var cliquePropose = new CliquePropose(client);
            return cliquePropose.SendRequestAsync(Settings.GetDefaultAccount(), true);
        }

        public override Type GetRequestType()
        {
            return typeof(CliquePropose);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
        }
    }

}
        