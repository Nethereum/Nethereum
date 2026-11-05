using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc.Handlers
{
    public class DebugBundlerClearStateHandler : RpcHandlerBase
    {
        private readonly IBundlerServiceExtended _bundler;

        public DebugBundlerClearStateHandler(IBundlerServiceExtended bundler)
        {
            _bundler = bundler;
        }

        public override string MethodName => "debug_bundler_clearState";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            try
            {
                await _bundler.ClearStateAsync();
                return Success(request.Id, "ok");
            }
            catch (Exception ex)
            {
                return BundlerRpcResponses.InternalError(request.Id, context, ex);
            }
        }
    }
}
