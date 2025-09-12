using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.HostWallet;
using Nethereum.Wallet.UI;
using Nethereum.RPC.Chain;

namespace Nethereum.Wallet.RpcRequests
{
    public class WalletAddEthereumChainHandler : RpcMethodHandlerBase
    {
        public override string MethodName => "wallet_addEthereumChain";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context)
        {
            try
            {
                var param = request.GetFirstParamAs<AddEthereumChainParameter>();
                if (param == null)
                    return InvalidParams(request.Id);

                var chainFeature = param.ToChainFeature();
                await context.AddChainAsync(chainFeature);

                return new RpcResponseMessage(request.Id, result: null);
            }
            catch (Exception ex)
            {
                return InternalError(request.Id, ex.Message);
            }
        }
    }

}
