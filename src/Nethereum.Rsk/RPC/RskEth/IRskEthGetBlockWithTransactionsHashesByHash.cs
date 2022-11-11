using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.Rsk.RPC.RskEth.DTOs;

namespace Nethereum.Rsk.RPC.RskEth
{
    public interface IRskEthGetBlockWithTransactionsHashesByHash
    {
        Task<RskBlockWithTransactionHashes> SendRequestAsync(string blockHash, object id = null);
        RpcRequest BuildRequest(string blockHash, object id = null);
    }
}