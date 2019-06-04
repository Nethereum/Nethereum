

using System;
using System.Threading.Tasks;
using Nethereum.Pantheon.RPC.Permissioning;
using Nethereum.JsonRpc.Client;
using Nethereum.Pantheon.IntegrationTests;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Pantheon.Tests.Testers
{

    public class PermRemoveAccountsFromWhitelistTester : RPCRequestTester<string>, IRPCRequestTester
    {
        public override async Task<string> ExecuteAsync(IClient client)
        {
            var permRemoveAccountsFromWhitelist = new PermRemoveAccountsFromWhitelist(client);
            return await permRemoveAccountsFromWhitelist.SendRequestAsync(new[] { Settings.GetDefaultAccount() });
        }

        public override Type GetRequestType()
        {
            return typeof(PermRemoveAccountsFromWhitelist);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }

}
        