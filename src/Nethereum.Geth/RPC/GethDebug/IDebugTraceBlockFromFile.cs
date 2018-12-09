using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugTraceBlockFromFile
    {
        RpcRequest BuildRequest(string filePath, object id = null);
        Task<JObject> SendRequestAsync(string filePath, object id = null);
    }
}