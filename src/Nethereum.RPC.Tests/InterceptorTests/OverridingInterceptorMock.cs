using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC.Tests.InterceptorTests
{
    public class OverridingInterceptorMock:RequestInterceptor
    {
        public override async Task<object> InterceptSendRequestAsync<T>(Func<RpcRequest, string, Task<T>> interceptedSendRequestAsync, RpcRequest request, string route = null)
        {
            if (request.Method == "eth_accounts")
            {
                return new string[] { "hello", "hello2"};
            }

            if (request.Method == "eth_getCode")
            {
                return "the code";
            }
            return await interceptedSendRequestAsync(request, route);
        }


        public override async Task<object> InterceptSendRequestAsync<T>(Func<string, string, object[], Task<T>> interceptedSendRequestAsync, string method, string route = null,
            params object[] paramList)
        {
            if (method == "eth_accounts")
            {
                return new string[] { "hello", "hello2"};
            }

            if (method == "eth_getCode")
            {
                return "the code";
            }

            return await interceptedSendRequestAsync(method, route, paramList);
        }
    }
}