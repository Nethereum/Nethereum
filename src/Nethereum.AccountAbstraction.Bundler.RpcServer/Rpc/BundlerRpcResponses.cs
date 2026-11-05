using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc
{
    public static class BundlerRpcResponses
    {
        public static RpcResponseMessage InternalError(object id, RpcContext context, Exception exception)
        {
            context.GetService<ILoggerFactory>()
                ?.CreateLogger("Nethereum.AccountAbstraction.Bundler.RpcServer")
                .LogError(exception, "Unexpected error in a bundler RPC handler.");

            return new RpcResponseMessage(id, new RpcError
            {
                Code = BundlerErrorCodes.InternalError,
                Message = "Internal error"
            });
        }
    }
}
