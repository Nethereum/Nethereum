using System;
using System.Threading.Tasks;

namespace Nethereum.JsonRpc.Client
{
    public abstract class RequestInterceptor
    {
        public virtual async Task<object> InterceptSendRequestAsync<T>(
            Func<RpcRequest, string, Task<T>> interceptedSendRequestAsync, RpcRequest request,
            string route = null)
        {
            return await interceptedSendRequestAsync(request, route).ConfigureAwait(false);
        }

        public virtual async Task InterceptSendRequestAsync(
            Func<RpcRequest, string, Task> interceptedSendRequestAsync, RpcRequest request,
            string route = null)
        {
            await interceptedSendRequestAsync(request, route).ConfigureAwait(false);
        }

        public virtual async Task<object> InterceptSendRequestAsync<T>(
            Func<string, string, object[], Task<T>> interceptedSendRequestAsync, string method,
            string route = null, params object[] paramList)
        {
            return await interceptedSendRequestAsync(method, route, paramList).ConfigureAwait(false);
        }

        public virtual async Task InterceptSendRequestAsync(
            Func<string, string, object[], Task> interceptedSendRequestAsync, string method,
            string route = null, params object[] paramList)
        {
            await interceptedSendRequestAsync(method, route, paramList).ConfigureAwait(false);
        }
    }
}