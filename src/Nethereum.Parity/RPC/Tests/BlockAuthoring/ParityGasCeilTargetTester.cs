
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;

namespace Nethereum.Parity.Test.Testers
{
    public class ParityGasCeilTargetTester : RPCRequestTester<HexBigInteger>, IRPCRequestTester
    {
        
        [Fact]
        public async void Should()
        {
            var result = await ExecuteAsync();
            Assert.True();
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
        