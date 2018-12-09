using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Eth.Filters
{
    public interface IEthGetFilterLogsForBlockOrTransaction
    {
        RpcRequest BuildRequest(HexBigInteger filterId, object id = null);
        Task<string[]> SendRequestAsync(HexBigInteger filterId, object id = null);
    }
}