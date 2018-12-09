using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Transactions
{
    public interface IEthGetTransactionByBlockHashAndIndex
    {
        RpcRequest BuildRequest(string blockHash, HexBigInteger transactionIndex, object id = null);
        Task<Transaction> SendRequestAsync(string blockHash, HexBigInteger transactionIndex, object id = null);
    }
}