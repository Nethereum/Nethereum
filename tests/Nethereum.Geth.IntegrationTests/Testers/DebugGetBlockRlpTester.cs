using System;
using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Geth.Tests.Testers
{
    public class DebugGetBlockRlpTester : RPCRequestTester<string>, IRPCRequestTester
    {
        public override async Task<string> ExecuteAsync(IClient client)
        {
            var debugGetBlockRlp = new DebugGetBlockRlp(client);
            return await debugGetBlockRlp.SendRequestAsync(Settings.GetBlockNumber()).ConfigureAwait(false);
        }

        public override Type GetRequestType()
        {
            return typeof(DebugGetBlockRlp);
        }

        [Fact]
        public async void ShouldReturnTheBlockRplAsAString()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
        }
    }
}