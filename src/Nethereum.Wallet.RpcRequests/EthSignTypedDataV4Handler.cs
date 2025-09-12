using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Wallet.UI;
using Newtonsoft.Json.Linq;
using Nethereum.ABI.EIP712;
using Nethereum.Util;

namespace Nethereum.Wallet.RpcRequests
{
    public class EthSignTypedDataV4Handler : RpcMethodHandlerBase
    {
        public override string MethodName => "eth_signTypedData_v4";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context)
        {
            var id = request.Id;

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

            var approved = await context.ShowSignPromptAsync(typedDataJson);
            if (string.IsNullOrEmpty(approved))
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

            var web3 = await context.GetWeb3Async();
            var signature = await web3.Eth.AccountSigning.SignTypedDataV4.SendRequestAsync(typedDataJson);

            return new RpcResponseMessage(id, signature);
        }
    }

}
