
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Development;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;

namespace Nethereum.Parity.Test.Testers
{
    public class ParityDevLogsTester : RPCRequestTester<string[]>, IRPCRequestTester
    {

        [Fact]
        public async void ShouldNotReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<string[]> ExecuteAsync(IClient client)
        {
            var parityDevLogs = new ParityDevLogs(client);
            return await parityDevLogs.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityDevLogs);
        }
    }
}
        