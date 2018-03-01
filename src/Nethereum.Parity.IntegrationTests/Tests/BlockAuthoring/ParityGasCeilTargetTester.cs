using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.BlockAuthoring;
using Xunit;

namespace Nethereum.Parity.IntegrationTests.Tests.BlockAuthoring
{
    public class ParityGasCeilTargetTester : RPCRequestTester<HexBigInteger>, IRPCRequestTester
    {
        public override async Task<HexBigInteger> ExecuteAsync(IClient client)
        {
            var parityGasCeilTarget = new ParityGasCeilTarget(client);
            return await parityGasCeilTarget.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityGasCeilTarget);
        }

        [Fact]
        public async void ShouldGetGasCeil()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }
    }
}