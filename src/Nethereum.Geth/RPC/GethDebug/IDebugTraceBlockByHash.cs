using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugTraceBlockByHash
    {
        RpcRequest BuildRequest(string hash, object id = null);
        Task<JObject> SendRequestAsync(string hash, object id = null);
    }
}