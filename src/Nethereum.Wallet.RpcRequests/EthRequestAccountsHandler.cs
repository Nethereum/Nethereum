using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Wallet.UI;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace Nethereum.Wallet.RpcRequests
{
    public class EthRequestAccountsHandler : RpcMethodHandlerBase
    {
        public override string MethodName => "eth_requestAccounts";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context)
        {
            var selectedAccount = context.SelectedAccount;
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
                    var approved = await context.RequestDappPermissionAsync(context.SelectedDapp!, selectedAccount).ConfigureAwait(false);
                    if (!approved)
                    {
                        return UserRejected(request.Id);
                    }
                }
            }

            var result = JArray.FromObject(new[] { selectedAccount });
            return new RpcResponseMessage(request.Id, result);
        }
    }

}
