#if !DOTNET35
using System.Threading.Tasks;

namespace Nethereum.JsonRpc.Client
{
    public abstract class ClientBase : IClient
    {

        public static int ConnectionTimeout { get; set; } = 5000;

        public RequestInterceptor OverridingRequestInterceptor { get; set; }

        public async Task<T> SendRequestAsync<T>(RpcRequest request, string route = null)
        {
            if (OverridingRequestInterceptor != null)
                return
                    (T)
                    await OverridingRequestInterceptor.InterceptSendRequestAsync(SendInnerRequestAync<T>, request, route)
                        .ConfigureAwait(false);
            return await SendInnerRequestAync<T>(request, route).ConfigureAwait(false);
        }

        public async Task<T> SendRequestAsync<T>(string method, string route = null, params object[] paramList)
        {
            if (OverridingRequestInterceptor != null)
                return
                    (T)
                    await OverridingRequestInterceptor.InterceptSendRequestAsync(SendInnerRequestAync<T>, method, route,
                        paramList).ConfigureAwait(false);
            return await SendInnerRequestAync<T>(method, route, paramList).ConfigureAwait(false);
        }

        public abstract Task SendRequestAsync(RpcRequest request, string route = null);
        public abstract Task SendRequestAsync(string method, string route = null, params object[] paramList);

        protected abstract Task<T> SendInnerRequestAync<T>(RpcRequest request, string route = null);

        protected abstract Task<T> SendInnerRequestAync<T>(string method, string route = null,
            params object[] paramList);
    }
}
#endif