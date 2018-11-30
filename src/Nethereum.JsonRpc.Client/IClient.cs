using System.Threading.Tasks;

namespace Nethereum.JsonRpc.Client
{
    public interface IClient : IBaseClient
    {
        Task<T> SendRequestAsync<T>(RpcRequest request, string route = null);
        Task<T> SendRequestAsync<T>(string method, string route = null, params object[] paramList);
    }
}