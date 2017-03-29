
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
    public class ParityGasCeilTargetTester : RPCRequestTester<HexBigInteger>, IRPCRequestTester
    {
        
        [Fact]
        public async void ShouldGetGasCeil()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<HexBigInteger> ExecuteAsync(IClient client)
        {
            var parityGasCeilTarget = new ParityGasCeilTarget(client);
            return await parityGasCeilTarget.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityGasCeilTarget);
        }
    }
}
        