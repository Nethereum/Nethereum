using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Wallet.UI;
using Newtonsoft.Json.Linq;

namespace Nethereum.Wallet.RpcRequests
{
    public class WalletRevokePermissionsHandler : RpcMethodHandlerBase
    {
        public override string MethodName => "wallet_revokePermissions";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context)
        {
            var result = new JArray();
            var account = context.SelectedWalletAccount?.Address;
            var origin = context.SelectedDapp?.Origin;

            if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(origin))
            {
                return new RpcResponseMessage(request.Id, result);
            }

            var normalizedAccount = account.Trim().ToLowerInvariant();
            var normalizedOrigin = origin.Trim().ToLowerInvariant();

            await context.DappPermissions.RevokeAsync(normalizedOrigin, normalizedAccount).ConfigureAwait(false);

            return new RpcResponseMessage(request.Id, result);
        }
    }
}
