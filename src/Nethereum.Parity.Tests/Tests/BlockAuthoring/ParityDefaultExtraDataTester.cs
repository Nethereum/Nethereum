
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.BlockAuthoring;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Tests;
using Xunit;

namespace Nethereum.Parity.Test.Testers
{
    public class ParityDefaultExtraDataTester : RPCRequestTester<string>, IRPCRequestTester
    {
        
        [Fact]
        public async void ShouldGetDefaultExtraData()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<string> ExecuteAsync(IClient client)
        {
            var parityDefaultExtraData = new ParityDefaultExtraData(client);
            return await parityDefaultExtraData.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(ParityDefaultExtraData);
        }
    }
}
        