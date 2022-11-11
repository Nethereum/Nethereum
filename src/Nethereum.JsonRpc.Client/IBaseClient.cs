using System.Threading.Tasks;

namespace Nethereum.JsonRpc.Client
{
    public interface IBaseClient
    {
#if !DOTNET35
        RequestInterceptor OverridingRequestInterceptor { get; set; }
#endif
        Task SendRequestAsync(RpcRequest request, string route = null);
        Task SendRequestAsync(string method, string route = null, params object[] paramList);
    }
}