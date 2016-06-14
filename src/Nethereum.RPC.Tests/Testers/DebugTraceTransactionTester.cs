
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;
using Newtonsoft.Json.Linq;
using Nethereum.RPC.DebugGeth.DTOs;

namespace Nethereum.RPC.Sample.Testers
{
    public class DebugTraceTransactionTester : RPCRequestTester<JObject>, IRPCRequestTester
    {

        [Fact]
        public async void Should()
        {
            var result = await ExecuteAsync(ClientFactory.GetClient());
            //Assert.True();
        }

        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var debugTraceTransaction = new DebugTraceTransaction(client);
            return await debugTraceTransaction.SendRequestAsync("0x31227fe8fdadbeb9e08626cfc2b869c2977fe8ab33ded0dc277d12ebce27c793", new TraceTransactionOptions());
        }

        public override Type GetRequestType()
        {
            return typeof(DebugTraceTransaction);
        }
    }
}
        