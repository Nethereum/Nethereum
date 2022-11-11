using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Accounts;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Parity.IntegrationTests.Tests.Accounts
{
    public class ParityAccountsInfoTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var parityAccountsInfo = new ParityAccountsInfo(client);
            return await parityAccountsInfo.SendRequestAsync().ConfigureAwait(false);
        }

        public override Type GetRequestType()
        {
            return typeof(ParityAccountsInfo);
        }

        [Fact]
        public async void ShouldGetInfo()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
        }
    }
}