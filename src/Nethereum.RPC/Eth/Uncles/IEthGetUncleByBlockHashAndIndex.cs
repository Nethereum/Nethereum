using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Uncles
{
    public interface IEthGetUncleByBlockHashAndIndex
    {
        RpcRequest BuildRequest(string blockHash, HexBigInteger uncleIndex, object id = null);
        Task<BlockWithTransactionHashes> SendRequestAsync(string blockHash, HexBigInteger uncleIndex, object id = null);
    }
}