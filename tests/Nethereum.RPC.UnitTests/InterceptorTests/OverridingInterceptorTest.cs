using System;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Xunit;

namespace Nethereum.RPC.UnitTests.InterceptorTests
{
    public class OverridingInterceptorTest
    {
        [Fact]
        public async void ShouldInterceptNoParamsRequest()
        {
            var client = new RpcClient(new Uri("http://localhost:8545/"));
      
            client.OverridingRequestInterceptor = new OverridingInterceptorMock();
            var ethAccounts = new EthAccounts(client);
            var accounts = await ethAccounts.SendRequestAsync().ConfigureAwait(false);
            Assert.True(accounts.Length == 2);
            Assert.Equal("hello", accounts[0]);
        }

        [Fact]
        public async void ShouldInterceptParamsRequest()
        {
            var client = new RpcClient(new Uri("http://localhost:8545/"));

            client.OverridingRequestInterceptor = new OverridingInterceptorMock();
            var ethGetCode = new EthGetCode(client);
            var code = await ethGetCode.SendRequestAsync("address").ConfigureAwait(false);
            Assert.Equal("the code", code);
        }
    }
}
