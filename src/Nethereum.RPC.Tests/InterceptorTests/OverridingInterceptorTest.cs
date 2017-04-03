using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Xunit;

namespace Nethereum.RPC.Tests.InterceptorTests
{
    public class OverridingInterceptorTest
    {
        [Fact]
        public async void ShouldInterceptNoParamsRequest()
        {
            var client = new RpcClient(new Uri("http://localhost:8545/"));
      
            client.OverridingRequestInterceptor = new OverridingInterceptorMock();
            var ethAccounts = new EthAccounts(client);
            var accounts = await ethAccounts.SendRequestAsync();
            Assert.True(accounts.Length == 2);
            Assert.Equal(accounts[0],"hello");
        }

        [Fact]
        public async void ShouldInterceptParamsRequest()
        {
            var client = new RpcClient(new Uri("http://localhost:8545/"));

            client.OverridingRequestInterceptor = new OverridingInterceptorMock();
            var ethGetCode = new EthGetCode(client);
            var code = await ethGetCode.SendRequestAsync("address");
            Assert.Equal(code, "the code");
        }
    }
}
