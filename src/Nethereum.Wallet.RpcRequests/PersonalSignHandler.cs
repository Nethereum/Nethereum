using System;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Wallet.UI;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.Wallet.RpcRequests
{
    public class PersonalSignHandler : RpcMethodHandlerBase
    {
        public override string MethodName => "personal_sign";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context)
        {
            if (context == null)
            {
                return InternalError(request.Id, "Wallet context unavailable");
            }

            var selectedAccount = context.SelectedWalletAccount;
            if (selectedAccount == null)
            {
                var enabledAccount = await context.EnableProviderAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(enabledAccount) || context.SelectedWalletAccount == null)
                {
                    return UserRejected(request.Id);
                }

                selectedAccount = context.SelectedWalletAccount;
            }

            var parameters = request.RawParameters as object[];
            if (parameters == null || parameters.Length < 2)
            {
                return InvalidParams(request.Id);
            }

            if (!TryExtractPersonalSignParams(parameters, out var message, out var address))
            {
                return InvalidParams(request.Id, "Unable to determine message and address");
            }

            if (!selectedAccount.Address.IsTheSameAddress(address))
            {
                return InvalidParams(request.Id, "Addresses do not match");
            }

            var promptContext = CreateSignaturePromptContext(message, address, context.SelectedDapp);
            string? signature;
            try
            {
                signature = await context.RequestPersonalSignAsync(promptContext).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return UserRejected(request.Id);
            }

            if (string.IsNullOrEmpty(signature))
            {
                return UserRejected(request.Id);
            }

            return new RpcResponseMessage(request.Id, signature);
        }

        private static bool TryExtractPersonalSignParams(object[] parameters, out string message, out string address)
        {
            message = string.Empty;
            address = string.Empty;

            string? first = parameters[0]?.ToString();
            string? second = parameters[1]?.ToString();

            var addressUtil = AddressUtil.Current;

            if (!string.IsNullOrEmpty(first) && !string.IsNullOrEmpty(second))
            {
                var firstIsAddress = first.IsValidEthereumAddressHexFormat();
                var secondIsAddress = second.IsValidEthereumAddressHexFormat();

                if (firstIsAddress && !secondIsAddress && parameters.Length >= 2)
                {
                    address = first;
                    message = second;
                    return true;
                }

                if (secondIsAddress)
                {
                    address = second;
                    message = first;
                    return true;
                }
            }

            return false;
        }

        private static SignaturePromptContext CreateSignaturePromptContext(string message, string address, DappConnectionContext? dapp)
        {
            var isHex = message.IsHex();
            string? decoded = null;

            if (isHex)
            {
                try
                {
                    var bytes = message.HexToByteArray();
                    decoded = Encoding.UTF8.GetString(bytes);
                }
                catch
                {
                    decoded = null;
                }
            }

            return new SignaturePromptContext
            {
                Method = "personal_sign",
                Message = message,
                DecodedMessage = decoded,
                IsMessageHex = isHex,
                Address = address,
                Origin = dapp?.Origin,
                DappName = dapp?.Title,
                DappIcon = dapp?.Icon
            };
        }

    }

}
