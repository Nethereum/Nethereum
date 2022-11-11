using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.BlockAuthoring;
using Xunit;

namespace Nethereum.Parity.IntegrationTests.Tests.BlockAuthoring
{
    public class ParityExtraDataTester : RPCRequestTester<string>, IRPCRequestTester
    {
        public override async Task<string> ExecuteAsync(IClient client)
        {
            var parityExtraData = new ParityExtraData(client);
            return await parityExtraData.SendRequestAsync().ConfigureAwait(false);
        }

        public override Type GetRequestType()
        {
            return typeof(ParityExtraData);
        }

        [Fact]
        public async void ShouldGetExtraData()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
        }
    }
}