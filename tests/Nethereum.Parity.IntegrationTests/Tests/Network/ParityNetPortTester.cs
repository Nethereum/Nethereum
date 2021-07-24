using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Network;
using Xunit;

namespace Nethereum.Parity.IntegrationTests.Tests.Network
{
    public class ParityNetPortTester : RPCRequestTester<int>, IRPCRequestTester
    {
        public override async Task<int> ExecuteAsync(IClient client)
        {
            var parityNetPort = new ParityNetPort(client);
            return await parityNetPort.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityNetPort);
        }

        [Fact]
        public async void ShouldNotReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.True(result > 0);
        }
    }
}