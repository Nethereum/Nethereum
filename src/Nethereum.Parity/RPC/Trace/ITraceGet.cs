using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Trace
{
    public interface ITraceGet
    {
        RpcRequest BuildRequest(string transactionHash, HexBigInteger[] index, object id = null);
        Task<JObject> SendRequestAsync(string transactionHash, HexBigInteger[] index, object id = null);
    }
}