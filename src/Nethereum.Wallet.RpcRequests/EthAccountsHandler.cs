using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Wallet.UI;
using Newtonsoft.Json.Linq;

namespace Nethereum.Wallet.RpcRequests
{
    public class EthAccountsHandler : RpcMethodHandlerBase
    {
        public override string MethodName => "eth_accounts";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context)
        {
            var selectedAccount = context.SelectedWalletAccount?.Address;
            if (string.IsNullOrWhiteSpace(selectedAccount))
            {
                return new RpcResponseMessage(request.Id, new JArray());
            }

            var origin = context.SelectedDapp?.Origin;
            if (!string.IsNullOrWhiteSpace(origin))
            {
                var normalizedOrigin = origin.Trim().ToLowerInvariant();
                var normalizedAccount = selectedAccount.Trim().ToLowerInvariant();

                if (!await context.DappPermissions.IsApprovedAsync(normalizedOrigin, normalizedAccount).ConfigureAwait(false))
                {
                    return new RpcResponseMessage(request.Id, new JArray());
                }
            }

            var result = JArray.FromObject(new[] { selectedAccount });
            return new RpcResponseMessage(request.Id, result);
        }
    }

}
