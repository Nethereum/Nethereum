using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.RPC.HostWallet;
using Nethereum.Wallet.UI;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Wallet.RpcRequests
{
    public class WalletAddEthereumChainHandler : RpcMethodHandlerBase
    {
        public override string MethodName => "wallet_addEthereumChain";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context)
        {
            try
            {
                var enabledAccount = await context.EnableProviderAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(enabledAccount) || context.SelectedWalletAccount == null)
                    return UserRejected(request.Id);

                var param = request.GetFirstParamAs<AddEthereumChainParameter>();
                if (param == null)
                    return InvalidParams(request.Id);

                if (param.ChainId == default)
                    return InvalidParams(request.Id, "Missing chainId");

                BigInteger chainIdValue;
                try
                {
                    chainIdValue = new HexBigInteger(param.ChainId).Value;
                }
                catch
                {
                    return InvalidParams(request.Id, "Invalid chainId");
                }

                var existingChain = context.Configuration.GetChain(chainIdValue);
                if (existingChain != null)
                {
                    var switchRequest = new ChainSwitchPromptRequest
                    {
                        ChainId = chainIdValue,
                        Chain = existingChain,
                        IsKnown = true,
                        AllowAdd = false,
                        Origin = context.SelectedDapp?.Origin,
                        DappName = context.SelectedDapp?.Title,
                        DappIcon = context.SelectedDapp?.Icon
                    };

                    var switchResult = await context.RequestChainSwitchAsync(switchRequest).ConfigureAwait(false);
                    if (!switchResult.Approved)
                        return UserRejected(request.Id);

                    if (!switchResult.SwitchSucceeded)
                    {
                        var message = string.IsNullOrWhiteSpace(switchResult.ErrorMessage)
                            ? "Failed to switch network"
                            : switchResult.ErrorMessage;
                        return InternalError(request.Id, message);
                    }

                    return new RpcResponseMessage(request.Id, result: null);
                }

                var promptRequest = new ChainAdditionPromptRequest
                {
                    Parameter = param,
                    SwitchAfterAdd = true,
                    Origin = context.SelectedDapp?.Origin,
                    DappName = context.SelectedDapp?.Title,
                    DappIcon = context.SelectedDapp?.Icon
                };

                var promptResult = await context.RequestChainAdditionAsync(promptRequest).ConfigureAwait(false);

                if (!promptResult.Approved)
                    return UserRejected(request.Id);

                return new RpcResponseMessage(request.Id, result: null);
            }
            catch (Exception ex)
            {
                return InternalError(request.Id, ex.Message);
            }
        }
    }

}
