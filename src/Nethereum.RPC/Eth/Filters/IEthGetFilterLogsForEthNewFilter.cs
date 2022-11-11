using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Filters
{
    public interface IEthGetFilterLogsForEthNewFilter
    {
        RpcRequest BuildRequest(HexBigInteger filterId, object id = null);
        Task<FilterLog[]> SendRequestAsync(HexBigInteger filterId, object id = null);
    }
}