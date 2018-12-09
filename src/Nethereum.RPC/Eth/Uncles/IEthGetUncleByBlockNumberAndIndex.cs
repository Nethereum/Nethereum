using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Uncles
{
    public interface IEthGetUncleByBlockNumberAndIndex
    {
        RpcRequest BuildRequest(BlockParameter blockParameter, HexBigInteger uncleIndex, object id = null);
        Task<BlockWithTransactionHashes> SendRequestAsync(BlockParameter blockParameter, HexBigInteger uncleIndex, object id = null);
    }
}