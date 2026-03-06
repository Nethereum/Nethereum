using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc.Handlers
{
    public class EthSupportedEntryPointsHandler : RpcHandlerBase
    {
        private readonly IBundlerService _bundler;

        public EthSupportedEntryPointsHandler(IBundlerService bundler)
        {
            _bundler = bundler;
        }

        public override string MethodName => "eth_supportedEntryPoints";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            try
            {
                var entryPoints = await _bundler.SupportedEntryPointsAsync();
                return Success(request.Id, entryPoints);
            }
            catch (Exception ex)
            {
                return Error(request.Id, -32603, $"Internal error: {ex.Message}");
            }
        }
    }
}
