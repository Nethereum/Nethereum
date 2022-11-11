using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace Nethereum.RPC.HostWallet
{
    public interface IWalletRequestPermissions
    {
        RpcRequest BuildRequest(string[] methods, object id = null);
        Task<JObject> SendRequestAsync(string[] methods, object id = null);

    }
}