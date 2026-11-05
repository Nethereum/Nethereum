using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Wallet.UI;
using Newtonsoft.Json.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Nethereum.Wallet.RpcRequests
{
    public class WalletSwitchEthereumChainHandler : RpcMethodHandlerBase
    {
        private static readonly Regex HexChainId = new Regex("^0x[0-9a-fA-F]+$", RegexOptions.Compiled);

        public override string MethodName => "wallet_switchEthereumChain";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context)
        {
            if (context == null)
            {
                return InternalError(request.Id, "Wallet context unavailable");
            }

            var dapp = context.SelectedDapp;
            var enabledAccount = await context.EnableProviderAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(enabledAccount) || context.SelectedWalletAccount == null)
            {
                return UserRejected(request.Id);
            }

            var rawChainId = request.GetFirstParamAs<JObject>()?["chainId"];
            if (rawChainId == null || rawChainId.Type == JTokenType.Null)
            {
                return InvalidParams(request.Id, "Missing chainId");
            }

            if (rawChainId.Type != JTokenType.String || !HexChainId.IsMatch(rawChainId.Value<string>()!))
            {
                return InvalidParams(request.Id, "Invalid chainId");
            }

            BigInteger chainId;
            try
            {
                chainId = new HexBigInteger(rawChainId.Value<string>()!).Value;
            }
            catch
            {
                return InvalidParams(request.Id, "Invalid chainId");
            }

            var promptRequest = new ChainSwitchPromptRequest
            {
                ChainId = chainId,
                Origin = dapp?.Origin,
                DappName = dapp?.Title,
                DappIcon = dapp?.Icon
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
