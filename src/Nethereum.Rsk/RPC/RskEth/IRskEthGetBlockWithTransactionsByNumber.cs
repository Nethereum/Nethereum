using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Rsk.RPC.RskEth.DTOs;

namespace Nethereum.Rsk.RPC.RskEth
{
    public interface IRskEthGetBlockWithTransactionsByNumber
    {
        Task<RskBlockWithTransactions> SendRequestAsync(BlockParameter blockParameter, object id = null);
        Task<RskBlockWithTransactions> SendRequestAsync(HexBigInteger number, object id = null);
        RpcRequest BuildRequest(HexBigInteger number, object id = null);
        RpcRequest BuildRequest(BlockParameter blockParameter, object id = null);
    }
}