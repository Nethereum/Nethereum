using System.Threading.Tasks;
using edjCase.JsonRpc.Core;

namespace RPCRequestResponseHandlers
{
    public interface IClient
    {
        Task<RpcResponse> SendRequestAsync(RpcRequest request, string route = null);
        Task<RpcResponse> SendRequestAsync(string method, string route = null, params object[] paramList);
    }
}