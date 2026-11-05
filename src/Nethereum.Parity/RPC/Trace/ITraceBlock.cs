using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Trace
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ClientExtensions, "parity-client", "ITraceBlock")]
    public interface ITraceBlock
    {
        RpcRequest BuildRequest(HexBigInteger blockNumber, object id = null);
        Task<JArray> SendRequestAsync(HexBigInteger blockNumber, object id = null);
    }
}