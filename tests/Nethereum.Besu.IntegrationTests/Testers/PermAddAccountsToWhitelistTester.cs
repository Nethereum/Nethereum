

using System;
using System.Threading.Tasks;
using Nethereum.Besu.RPC.Permissioning;
using Nethereum.JsonRpc.Client;
using Nethereum.Besu.IntegrationTests;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Besu.Tests.Testers
{

    public class PermAddAccountsToWhitelistTester : RPCRequestTester<string>, IRPCRequestTester
    {
        public override Task<string> ExecuteAsync(IClient client)
        {
            var permAddAccountsToWhitelist = new PermAddAccountsToWhitelist(client);
            return permAddAccountsToWhitelist.SendRequestAsync(new[] { Settings.GetDefaultAccount() });
        }

        public override Type GetRequestType()
        {
            return typeof(PermAddAccountsToWhitelist);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
        }
    }

}
        