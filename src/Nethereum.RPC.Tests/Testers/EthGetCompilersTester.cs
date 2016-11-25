using System;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Compilation;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
    public class EthGetCompilersTester : RPCRequestTester<string[]>
    {
        [Fact]
        public async void ShouldReturnCompilers()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
            //we need at least solidity configured
            Assert.True(result.Contains("Solidity"));

        }

        public override async Task<string[]> ExecuteAsync(IClient client)
        {
            var ethGetCompilers = new EthGetCompilers(client);
            return await ethGetCompilers.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(EthGetCompilers);
        }
    }
}