using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Wallet.UI;
using Nethereum.Wallet.Hosting;
using System.Threading.Tasks;

namespace Nethereum.Wallet.RpcRequests
{
    public abstract class RpcMethodHandlerBase : IRpcMethodHandler
    {
        public abstract string MethodName { get; }
        public abstract Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context);

        protected RpcResponseMessage MethodNotImplemented(object id) => RpcErrors.MethodNotFound(id);
        protected RpcResponseMessage InvalidParams(object id, string? message = null) => RpcErrors.InvalidParams(id, message);
        protected RpcResponseMessage UserRejected(object id) => RpcErrors.UserRejected(id);
        protected RpcResponseMessage InternalError(object id, string? message = null) => RpcErrors.InternalError(id, message);
    }

}
