using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC.HostWallet
{
    public interface IWalletGetPermissions : IGenericRpcRequestResponseHandlerNoParam<JObject>
    {

    }
}
