using System;
using System.Collections.Generic;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Wallet.UI;
using Newtonsoft.Json.Linq;

namespace Nethereum.Wallet.RpcRequests
{
    public class WalletRequestPermissionsHandler : RpcMethodHandlerBase
    {
        public override string MethodName => "wallet_requestPermissions";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context)
        {
            var selectedAccount = context.SelectedWalletAccount?.Address;
            if (string.IsNullOrWhiteSpace(selectedAccount))
            {
                return new RpcResponseMessage(request.Id, new JArray());
            }

            var dapp = context.SelectedDapp;
            var origin = dapp?.Origin;
            if (!string.IsNullOrWhiteSpace(origin))
            {
                var normalizedOrigin = origin.Trim().ToLowerInvariant();
                var normalizedAccount = selectedAccount.Trim().ToLowerInvariant();

                if (!await context.DappPermissions.IsApprovedAsync(normalizedOrigin, normalizedAccount).ConfigureAwait(false))
                {
                    var approved = await context.RequestDappPermissionAsync(dapp!, selectedAccount).ConfigureAwait(false);
                    if (!approved)
                    {
                        return UserRejected(request.Id);
                    }
                }
            }

            var permissions = new[]
            {
                new Dictionary<string, object>
                {
                    { "parentCapability", "eth_accounts" },
                    { "caveats", Array.Empty<object>() }
                }
            };
            var result = JArray.FromObject(permissions);
            return new RpcResponseMessage(request.Id, result);
        }
    }

}
