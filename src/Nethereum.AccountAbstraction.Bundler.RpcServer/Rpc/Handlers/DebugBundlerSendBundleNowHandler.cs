using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc.Handlers
{
    public class DebugBundlerSendBundleNowHandler : RpcHandlerBase
    {
        private readonly IBundlerServiceExtended _bundler;

        public DebugBundlerSendBundleNowHandler(IBundlerServiceExtended bundler)
        {
            _bundler = bundler;
        }

        public override string MethodName => "debug_bundler_sendBundleNow";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            try
            {
                var txHash = await _bundler.FlushAsync();
                return Success(request.Id, txHash ?? "0x");
            }
            catch (Exception ex)
            {
                return Error(request.Id, BundlerErrorCodes.InternalError, $"Internal error: {ex.Message}");
            }
        }
    }
}
