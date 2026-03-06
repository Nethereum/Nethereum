using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc.Handlers
{
    public class DebugBundlerFlushHandler : RpcHandlerBase
    {
        private readonly IBundlerServiceExtended _bundler;

        public DebugBundlerFlushHandler(IBundlerServiceExtended bundler)
        {
            _bundler = bundler;
        }

        public override string MethodName => "debug_bundler_sendBundleNow";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            try
            {
                var txHash = await _bundler.FlushAsync();
                return Success(request.Id, txHash);
            }
            catch (Exception ex)
            {
                return Error(request.Id, -32603, $"Internal error: {ex.Message}");
            }
        }
    }
}
