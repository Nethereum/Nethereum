using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Wallet.UI;
using Newtonsoft.Json.Linq;

namespace Nethereum.Wallet.RpcRequests
{
    public class WalletGetPermissionsHandler : RpcMethodHandlerBase
    {
        public override string MethodName => "wallet_getPermissions";

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

            var permissions = await context.DappPermissions.GetPermissionsAsync(normalizedAccount).ConfigureAwait(false);

            foreach (var permission in permissions)
            {
                if (!string.Equals(permission.Origin, normalizedOrigin, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var permissionObj = new JObject
                {
                    ["parentCapability"] = "eth_accounts",
                    ["caveats"] = new JArray(
                        new JObject
                        {
                            ["type"] = "restrictReturnedAccounts",
                            ["value"] = new JArray(account)
                        }
                    )
                };

                result.Add(permissionObj);
            }

            return new RpcResponseMessage(request.Id, result);
        }
    }
}
