using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.CoreChain.Rpc
{
    public interface IRpcHandler
    {
        string MethodName { get; }
        Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context);
    }
}
