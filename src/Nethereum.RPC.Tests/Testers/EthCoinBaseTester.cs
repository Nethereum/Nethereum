using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.Compilation;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.RPC.Tests.Testers
{
  
    public class EthCoinBaseTester : RPCRequestTester<string>, IRPCRequestTester
    {
        [Fact]
        public async void ShouldReturnCoinBaseAccount()
        {
            var result = await ExecuteAsync();
            Assert.NotNull(result);
        }

        public override async Task<string> ExecuteAsync(IClient client)
        {
            var ethCoinBase = new EthCoinBase(client);
            return await ethCoinBase.SendRequestAsync();
        }

        public override Type GetRequestType()
        {
            return typeof(EthCoinBase);
        }
    }
}