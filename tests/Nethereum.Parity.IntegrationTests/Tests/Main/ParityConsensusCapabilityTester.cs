using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Admin;
using Xunit;

namespace Nethereum.Parity.IntegrationTests.Tests.Main
{
    public class ParityConsensusCapabilityTester : RPCRequestTester<string>, IRPCRequestTester
    {
        public override async Task<string> ExecuteAsync(IClient client)
        {
            var parityConsensusCapability = new ParityConsensusCapability(client);
            return await parityConsensusCapability.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityConsensusCapability);
        }

        [Fact]
        public async void ShouldNotReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }
}