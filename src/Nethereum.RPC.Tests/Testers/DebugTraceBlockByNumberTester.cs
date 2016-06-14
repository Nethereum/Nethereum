
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC.Sample.Testers
{
    public class DebugTraceBlockByNumberTester : RPCRequestTester<JObject>, IRPCRequestTester
    {


        [Fact]
        public async void ShouldDecodeTheBlockRplAsJObject()
        {
            var result = await ExecuteAsync(ClientFactory.GetClient());
            Assert.NotNull(result);
        }

        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var debugTraceBlockByNumber = new DebugTraceBlockByNumber(client);
            return await debugTraceBlockByNumber.SendRequestAsync(10000);
        }

        public override Type GetRequestType()
        {
            return typeof(DebugTraceBlockByNumber);
        }
    }
}
        