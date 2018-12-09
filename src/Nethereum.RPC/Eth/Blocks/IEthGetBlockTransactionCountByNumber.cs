using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Blocks
{
    public interface IEthGetBlockTransactionCountByNumber
    {
        RpcRequest BuildRequest(BlockParameter block, object id = null);
        Task<HexBigInteger> SendRequestAsync(object id = null);
        Task<HexBigInteger> SendRequestAsync(BlockParameter block, object id = null);
    }
}