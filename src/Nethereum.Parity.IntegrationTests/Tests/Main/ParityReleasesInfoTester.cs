using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Admin;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Parity.IntegrationTests.Tests.Main
{
    public class ParityReleasesInfoTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var parityReleasesInfo = new ParityReleasesInfo(client);
            return await parityReleasesInfo.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityReleasesInfo);
        }

        [Fact]
        public async void ShouldNotReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }
}