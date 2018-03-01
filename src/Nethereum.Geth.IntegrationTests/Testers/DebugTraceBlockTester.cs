using System;
using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Geth.Tests.Testers
{
    public class DebugTraceBlockTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var debugTraceBlock = new DebugTraceBlock(client);
            var debugGetBlockRlp = new DebugGetBlockRlp(client);
            var rlp = await debugGetBlockRlp.SendRequestAsync(Settings.GetBlockNumber());
            return await debugTraceBlock.SendRequestAsync("0x" + rlp);
        }

        public override Type GetRequestType()
        {
            return typeof(DebugTraceBlock);
        }

        [Fact]
        public async void ShouldDecodeTheBlockRplAsJObject()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }
}