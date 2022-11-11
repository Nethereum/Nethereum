using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{

    public class EthGetCodeTester : RPCRequestTester<string>
    {
        [Fact]
        public async void Should()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
            //we want some code
            Assert.True(result.Length > "0x".Length);

        }

        public override async Task<string> ExecuteAsync(IClient client)
        {
            var ethGetCode = new EthGetCode(client);
            return await ethGetCode.SendRequestAsync(Settings.GetContractAddress()).ConfigureAwait(false);
        }

        public override Type GetRequestType()
        {
            return typeof(EthGetCode);
        }
    }
}