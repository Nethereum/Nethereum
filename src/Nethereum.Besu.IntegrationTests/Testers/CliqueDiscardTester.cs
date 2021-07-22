

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

    public class CliqueDiscardTester : RPCRequestTester<bool>, IRPCRequestTester
    {
        public override Task<bool> ExecuteAsync(IClient client)
        {
            var address = Settings.GetDefaultAccount();
            var cliqueDiscard = new CliqueDiscard(client);
            return cliqueDiscard.SendRequestAsync(address);
        }

        public override Type GetRequestType()
        {
            return typeof(CliqueDiscard);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.False(true);
        }
    }

}
        