
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Admin;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.Test.Testers
{
    public class ParityReleasesInfoTester : RPCRequestTester<JObject>, IRPCRequestTester
    {

        [Fact]
        public async void ShouldNotReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var parityReleasesInfo = new ParityReleasesInfo(client);
            return await parityReleasesInfo.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityReleasesInfo);
        }
    }
}
        