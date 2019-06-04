

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

    public class PermAddAccountsToWhitelistTester : RPCRequestTester<string>, IRPCRequestTester
    {
        public override async Task<string> ExecuteAsync(IClient client)
        {
            var permAddAccountsToWhitelist = new PermAddAccountsToWhitelist(client);
            return await permAddAccountsToWhitelist.SendRequestAsync(new[] { Settings.GetDefaultAccount() });
        }

        public override Type GetRequestType()
        {
            return typeof(PermAddAccountsToWhitelist);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }

}
        