using System.Threading.Tasks;
using edjCase.JsonRpc.Core;

namespace Nethereum.JsonRpc.Client
{
    public interface IClient
    {
        Task<RpcResponse> SendRequestAsync(RpcRequest request, string route = null);
        Task<RpcResponse> SendRequestAsync(string method, string route = null, params object[] paramList);
    }
}