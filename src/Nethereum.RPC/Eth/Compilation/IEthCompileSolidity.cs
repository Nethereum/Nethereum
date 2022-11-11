using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.RPC.Eth.Compilation
{
    public interface IEthCompileSolidity
    {
        RpcRequest BuildRequest(string contractCode, object id = null);
        Task<JToken> SendRequestAsync(string contractCode, object id = null);
    }
}