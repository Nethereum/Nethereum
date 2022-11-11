using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Filters
{
    public interface IEthNewFilter
    {
        RpcRequest BuildRequest(NewFilterInput newFilterInput, object id = null);
        Task<HexBigInteger> SendRequestAsync(NewFilterInput newFilterInput, object id = null);
    }
}