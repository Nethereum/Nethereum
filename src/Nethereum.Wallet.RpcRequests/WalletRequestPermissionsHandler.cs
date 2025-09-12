using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Wallet.UI;
using Newtonsoft.Json.Linq;

namespace Nethereum.Wallet.RpcRequests
{
    public class WalletRequestPermissionsHandler : RpcMethodHandlerBase
    {
        public override string MethodName => "wallet_requestPermissions";

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, IWalletContext context)
        {
            var permissions = new[]
            {
            new Dictionary<string, object>
            {
                { "parentCapability", "eth_accounts" },
                { "caveats", Array.Empty<object>() }
            }
        };
            var result = JArray.FromObject(permissions);
            return Task.FromResult(new RpcResponseMessage(request.Id, result));
        }
    }

}
