using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Besu.RPC.Debug
{
    public interface IDebugTraceTransaction
    {
        Task<JObject> SendRequestAsync(string transactionHash, object id = null);
        RpcRequest BuildRequest(string transactionHash, object id = null);
    }
}