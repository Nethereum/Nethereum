
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;
using Nethereum.Hex.HexTypes;
using Nethereum.Parity.RPC.BlockAuthoring;

namespace Nethereum.Parity.Test.Testers
{
    public class ParityGasFloorTargetTester : RPCRequestTester<HexBigInteger>, IRPCRequestTester
    {
        
        [Fact]
        public async void ShouldGetGasFloorTarget()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<HexBigInteger> ExecuteAsync(IClient client)
        {
            var parityGasFloorTarget = new ParityGasFloorTarget(client);
            return await parityGasFloorTarget.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityGasFloorTarget);
        }
    }
}
        