using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.Debug
{
    public interface IDebugTraceBlockByNumber
    {
        RpcRequest BuildRequest(ulong blockNumber, object id = null);
        Task<JObject> SendRequestAsync(ulong blockNumber, object id = null);
    }
}