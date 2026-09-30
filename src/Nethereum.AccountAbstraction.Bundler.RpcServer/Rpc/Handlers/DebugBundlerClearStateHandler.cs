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
                return Error(request.Id, BundlerErrorCodes.InternalError, $"Internal error: {ex.Message}");
            }
        }
    }
}
