

using System;
using System.Threading.Tasks;
using Nethereum.Pantheon.RPC.Debug;
using Nethereum.JsonRpc.Client;
using Nethereum.Pantheon.IntegrationTests;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Pantheon.Tests.Testers
{

    public class DebugTraceTransactionTester : RPCRequestTester<JObject>, IRPCRequestTester
    {
        public override async Task<JObject> ExecuteAsync(IClient client)
        {
            var debugTraceTransaction = new DebugTraceTransaction(client);
            return await debugTraceTransaction.SendRequestAsync(Settings.GetTransactionHash());
        }

        public override Type GetRequestType()
        {
            return typeof(DebugTraceTransaction);
        }

        [Fact]
        public async void ShouldReturnNotNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }

}
        