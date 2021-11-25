using System;
using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Geth.Tests.Testers
{
    public class DebugTraceBlockTester : RPCRequestTester<JArray>, IRPCRequestTester
    {
        public override async Task<JArray> ExecuteAsync(IClient client)
        {
            var debugTraceBlock = new DebugTraceBlock(client);
            var debugGetBlockRlp = new DebugGetBlockRlp(client);
            var rlp = await debugGetBlockRlp.SendRequestAsync(Settings.GetBlockNumber());
            return await debugTraceBlock.SendRequestAsync("0x" + rlp, new TraceTransactionOptions());
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