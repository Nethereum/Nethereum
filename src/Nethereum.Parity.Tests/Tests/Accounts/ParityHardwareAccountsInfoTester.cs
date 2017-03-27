
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;
using Nethereum.Parity.RPC.Main;

namespace Nethereum.Parity.Test.Testers
{
    public class ParityHardwareAccountsInfoTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        
        [Fact]
        public async void Should()
        {
            var result = await ExecuteAsync();
            Assert.True();
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
        