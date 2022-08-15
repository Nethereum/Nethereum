using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC.HostWallet
{
    public class WalletGetPermissions : GenericRpcRequestResponseHandlerNoParam<JObject>
    {
        public WalletGetPermissions() : this(null)
        {
        }

        public WalletGetPermissions(IClient client) : base(client, ApiMethods.wallet_getPermissions.ToString())
        {

        }
    }
}
