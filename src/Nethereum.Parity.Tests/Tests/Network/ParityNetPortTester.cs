
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;
using Nethereum.Parity.RPC.Network;

namespace Nethereum.Parity.Test.Testers
{
    public class ParityNetPortTester : RPCRequestTester<int>, IRPCRequestTester
    {

        [Fact]
        public async void ShouldNotReturnNull()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<int> ExecuteAsync(IClient client)
        {
            var parityNetPort = new ParityNetPort(client);
            return await parityNetPort.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityNetPort);
        }
    }
}
        