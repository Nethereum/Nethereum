using System.Threading.Tasks;

namespace Nethereum.RPC.Infrastructure
{
    public interface IGenericRpcRequestResponseHandlerNoParam<TResponse>
    {
        Task<TResponse> SendRequestAsync(object id = null);
    }
}