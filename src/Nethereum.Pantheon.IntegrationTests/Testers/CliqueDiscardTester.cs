

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

    public class CliqueDiscardTester : RPCRequestTester<bool>, IRPCRequestTester
    {
        public override async Task<bool> ExecuteAsync(IClient client)
        {
            var address = Settings.GetDefaultAccount();
            var cliqueDiscard = new CliqueDiscard(client);
            return await cliqueDiscard.SendRequestAsync(address);
        }

        public override Type GetRequestType()
        {
            return typeof(CliqueDiscard);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync();
            Assert.False(true);
        }
    }

}
        