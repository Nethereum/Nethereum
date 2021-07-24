

using System;
using System.Threading.Tasks;
using Nethereum.Besu.RPC.Debug;
using Nethereum.JsonRpc.Client;
using Nethereum.Besu.IntegrationTests;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Besu.Tests.Testers
{

    public class DebugTraceTransactionTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        public override Task<JObject> ExecuteAsync(IClient client)
        {
            var debugTraceTransaction = new DebugTraceTransaction(client);
            return debugTraceTransaction.SendRequestAsync(Settings.GetTransactionHash());
        }

        public override Type GetRequestType()
        {
            return typeof(DebugTraceTransaction);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
        }
    }

}
        