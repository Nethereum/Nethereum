using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Development;
using Xunit;

namespace Nethereum.Parity.IntegrationTests.Tests.Development
{
    public class ParityDevLogsLevelsTester : RPCRequestTester<string>, IRPCRequestTester
    {
        public override async Task<string> ExecuteAsync(IClient client)
        {
            var parityDevLogsLevels = new ParityDevLogsLevels(client);
            return await parityDevLogsLevels.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityDevLogsLevels);
        }

        [Fact]
        public async void ShouldNotReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }
}