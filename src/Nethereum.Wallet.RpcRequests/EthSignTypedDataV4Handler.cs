using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Wallet.UI;
using Newtonsoft.Json.Linq;
using Nethereum.ABI.EIP712;
using Nethereum.Util;
using System.Numerics;

namespace Nethereum.Wallet.RpcRequests
{
    public class EthSignTypedDataV4Handler : RpcMethodHandlerBase
    {
        public override string MethodName => "eth_signTypedData_v4";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context)
        {
            var id = request.Id;

            var enabledAccount = await context.EnableProviderAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(enabledAccount) || context.SelectedWalletAccount == null)
            {
                return UserRejected(id);
            }

            var parameters = request.RawParameters as object[];
            if (parameters == null || parameters.Length != 2)
                return InvalidParams(id);

            var address = parameters[0]?.ToString().Trim();
            var typedDataJson = parameters[1]?.ToString();
            if (address == null || typedDataJson == null)
                return InvalidParams(id);

            TypedDataRaw typedDataRaw;
            try
            {
                typedDataRaw = TypedDataRawJsonConversion.DeserialiseJsonToRawTypedData(typedDataJson);
            }
            catch (Exception ex)
            {
                return InvalidParams(id, $"Invalid EIP-712 structure: {ex.Message}");
            }

            var domainChainId = typedDataRaw.GetChainIdFromDomain();
            var contextChainId = context.ChainId?.Value;

            if (domainChainId != null && contextChainId != null && domainChainId != contextChainId)
            {
                return InvalidParams(id, $"Domain chainId {domainChainId} does not match active context chainId {contextChainId}");
            }

            var (domainName, domainVersion, verifyingContract, chainIdValue) = ExtractDomainMetadata(typedDataRaw);

            var promptRequest = new TypedDataSignPromptContext
            {
                Address = address,
                TypedDataJson = typedDataJson,
                Origin = context.SelectedDapp?.Origin,
                DappName = context.SelectedDapp?.Title,
                DappIcon = context.SelectedDapp?.Icon,
                DomainName = domainName,
                DomainVersion = domainVersion,
                VerifyingContract = verifyingContract,
                PrimaryType = typedDataRaw.PrimaryType,
                ChainId = chainIdValue
            };

            var approved = await context.RequestTypedDataSignAsync(promptRequest).ConfigureAwait(false);
            if (!approved)
            {
                return UserRejected(id);
            }

            var account = await context.GetSelectedAccountAsync();
            if (account == null)
            {
                return InvalidParams(id, "No account selected");
            }

            if(!account.Address.IsTheSameAddress(address))
            {
                return InvalidParams(id, "Addresses do not match");
            }

            var web3 = await context.GetWalletWeb3Async();
            var signature = await web3.Eth.AccountSigning.SignTypedDataV4.SendRequestAsync(typedDataJson);

            return new RpcResponseMessage(id, signature);
        }

        private static (string? name, string? version, string? verifyingContract, string? chainId) ExtractDomainMetadata(TypedDataRaw typedData)
        {
            string? name = null;
            string? version = null;
            string? verifyingContract = null;
            string? chainId = null;

            if (typedData.Types.TryGetValue("EIP712Domain", out var domainMembers) && typedData.DomainRawValues != null)
            {
                for (var i = 0; i < domainMembers.Length && i < typedData.DomainRawValues.Length; i++)
                {
                    var descriptor = domainMembers[i];
                    var value = typedData.DomainRawValues[i]?.Value;

                    switch (descriptor.Name)
                    {
                        case "name":
                            name = value?.ToString();
                            break;
                        case "version":
                            version = value?.ToString();
                            break;
                        case "verifyingContract":
                            verifyingContract = value?.ToString();
                            break;
                        case "chainId":
                            if (value is BigInteger chainIdValue)
                            {
                                chainId = chainIdValue.ToString();
                            }
                            else
                            {
                                chainId = value?.ToString();
                            }
                            break;
                    }
                }
            }

            return (name, version, verifyingContract, chainId);
        }
    }

}
