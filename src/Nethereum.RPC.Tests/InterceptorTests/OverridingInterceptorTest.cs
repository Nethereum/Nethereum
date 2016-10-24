using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.RPC.Tests.InterceptorTests
{

    public class OverridingInterceptorMock:RequestInterceptor
    {
        public override async Task<RpcResponse> InterceptSendRequestAsync(Func<RpcRequest, string, Task<RpcResponse>> interceptedSendRequestAsync, RpcRequest request, string route = null)
        {
          
            if (request.Method == "eth_accounts")
            {
                return BuildResponse(new string[] { "hello", "hello2"}, route);
            }

            if (request.Method == "eth_getCode")
            {
                return BuildResponse("the code", route);
            }
            return await interceptedSendRequestAsync(request, route);
        }

        public RpcResponse BuildResponse(object results, string route = null)
        {
            var token = JToken.FromObject(results);
            return new RpcResponse(route, token);
        }

        public override async Task<RpcResponse> InterceptSendRequestAsync(Func<string, string, object[], Task<RpcResponse>> interceptedSendRequestAsync, string method, string route = null,
            params object[] paramList)
        {
            if (method == "eth_getCode")
            {
                return BuildResponse("the code", route);
            }

            if (method == "eth_accounts")
            {
                return BuildResponse(new string[] { "hello", "hello2" }, route);
            }

            return await interceptedSendRequestAsync(method, route, paramList);
        }
    }

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
