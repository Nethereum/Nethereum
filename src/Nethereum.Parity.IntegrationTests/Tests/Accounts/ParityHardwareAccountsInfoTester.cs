using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Accounts;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Parity.IntegrationTests.Tests.Accounts
{
    public class ParityHardwareAccountsInfoTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var parityHardwareAccountsInfo = new ParityHardwareAccountsInfo(client);
            return await parityHardwareAccountsInfo.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityHardwareAccountsInfo);
        }

        [Fact]
        public async Task ShouldSucceed()
        {
            try
            {
                var result = await ExecuteAsync();
                Assert.NotNull(result);
            }
            catch (RpcResponseException exception)
            {
                Assert.Equal(-32023, exception.RpcError.Code);
                Assert.Equal("Custom(\"No hardware wallet accounts were found\")", exception.RpcError.Data.Value<string>());
            }
        }
    }
}