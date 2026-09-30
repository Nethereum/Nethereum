using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc.Handlers
{
    public class DebugBundlerClearReputationHandler : RpcHandlerBase
    {
        private readonly IBundlerServiceExtended _bundler;

        public DebugBundlerClearReputationHandler(IBundlerServiceExtended bundler)
        {
            _bundler = bundler;
        }

        public override string MethodName => "debug_bundler_clearReputation";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            try
            {
                await _bundler.ClearReputationAsync();
                return Success(request.Id, "ok");
            }
            catch (Exception ex)
            {
                return Error(request.Id, BundlerErrorCodes.InternalError, $"Internal error: {ex.Message}");
            }
        }
    }
}
