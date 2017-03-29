
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Accounts;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.Test.Testers
{
    public class ParityHardwareAccountsInfoTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        
        [Fact]
        public async void ShouldGetHardwareAccountsInfo()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var parityHardwareAccountsInfo = new ParityHardwareAccountsInfo(client);
            return await parityHardwareAccountsInfo.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityHardwareAccountsInfo);
        }
    }
}
        