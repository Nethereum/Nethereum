using System;
using System.Threading.Tasks;
using EdjCase.JsonRpc.Core;

namespace Nethereum.JsonRpc.Client
{
    public abstract class RequestInterceptor
    {
        public abstract Task<RpcResponse> InterceptSendRequestAsync(
            Func<RpcRequest, string, Task<RpcResponse>> interceptedSendRequestAsync, RpcRequest request,
            string route = null);

        public abstract Task<RpcResponse> InterceptSendRequestAsync(
            Func<string, string, object[], Task<RpcResponse>> interceptedSendRequestAsync, string method,
            string route = null, params object[] paramList);
      
    }
}