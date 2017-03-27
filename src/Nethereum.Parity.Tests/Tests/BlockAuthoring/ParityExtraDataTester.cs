
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;
using Nethereum.Parity.RPC.Main;

namespace Nethereum.Parity.Test.Testers
{
    public class ParityExtraDataTester : RPCRequestTester<string>, IRPCRequestTester
    {
        
        [Fact]
        public async void Should()
        {
            var result = await ExecuteAsync();
            Assert.True();
        }

        public override async Task<string> ExecuteAsync(IClient client)
        {
            var parityExtraData = new ParityExtraData(client);
            return await parityExtraData.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityExtraData);
        }
    }
}
        