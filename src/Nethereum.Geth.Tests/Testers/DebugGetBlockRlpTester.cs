using System;
using System.Threading.Tasks;
using Nethereum.Geth.RPC.Debug;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Xunit;

namespace Nethereum.Geth.Tests.Testers
{
    public class DebugGetBlockRlpTester : RPCRequestTester<string>, IRPCRequestTester
    {

        [Fact]
        public async void ShouldReturnTheBlockRplAsAString()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<string> ExecuteAsync(IClient client)
        {
            var debugGetBlockRlp = new DebugGetBlockRlp(client);
            return await debugGetBlockRlp.SendRequestAsync(Settings.GetBlockNumber());
        }

        public override Type GetRequestType()
        {
            return typeof(DebugGetBlockRlp);
        }
    }
}
        