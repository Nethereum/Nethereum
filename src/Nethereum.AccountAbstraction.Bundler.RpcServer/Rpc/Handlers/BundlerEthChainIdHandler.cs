using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc.Handlers
{
    public class BundlerEthChainIdHandler : RpcHandlerBase
    {
        private readonly IBundlerService _bundler;

        public BundlerEthChainIdHandler(IBundlerService bundler)
        {
            _bundler = bundler;
        }

        public override string MethodName => "eth_chainId";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            try
            {
                var chainId = await _bundler.ChainIdAsync();
                return Success(request.Id, ToHex(chainId));
            }
            catch (Exception ex)
            {
                return Error(request.Id, -32603, $"Internal error: {ex.Message}");
            }
        }
    }
}
