using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.HostWallet;
using Nethereum.Wallet.UI;
using System.Threading.Tasks;

namespace Nethereum.Wallet.RpcRequests
{
    public class WalletSwitchEthereumChainHandler : RpcMethodHandlerBase
    {
        public override string MethodName => "wallet_switchEthereumChain";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context)
        {
            if (context == null)
            {
                return InternalError(request.Id, "Wallet context unavailable");
            }

            var enabledAccount = await context.EnableProviderAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(enabledAccount) || context.SelectedWalletAccount == null)
            {
                return UserRejected(request.Id);
            }

            var param = request.GetFirstParamAs<SwitchEthereumChainParameter>();
            if (param?.ChainId == null)
            {
                return InvalidParams(request.Id, "Missing chainId");
            }

            var chainId = param.ChainId.Value;

            var promptRequest = new ChainSwitchPromptRequest
            {
                ChainId = chainId,
                Origin = context.SelectedDapp?.Origin,
                DappName = context.SelectedDapp?.Title,
                DappIcon = context.SelectedDapp?.Icon
            };

            var promptResult = await context.RequestChainSwitchAsync(promptRequest).ConfigureAwait(false);
            if (!promptResult.Approved)
            {
                return UserRejected(request.Id);
            }

            if (!promptResult.SwitchSucceeded)
            {
                var message = string.IsNullOrWhiteSpace(promptResult.ErrorMessage)
                    ? "Failed to switch network"
                    : promptResult.ErrorMessage;
                return InternalError(request.Id, message);
            }

            return new RpcResponseMessage(request.Id, result: null);
        }
    }

}
