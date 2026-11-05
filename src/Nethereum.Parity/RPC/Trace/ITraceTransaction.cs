using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Trace
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ClientExtensions, "parity-client", "ITraceTransaction")]
    public interface ITraceTransaction
    {
        RpcRequest BuildRequest(string transactionHash, object id = null);
        Task<JArray> SendRequestAsync(string transactionHash, object id = null);
    }
}
