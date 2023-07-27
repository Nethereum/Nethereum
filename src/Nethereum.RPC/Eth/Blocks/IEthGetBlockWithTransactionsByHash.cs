using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Blocks
{
    public interface IEthGetBlockWithTransactionsByHash
    {
        RpcRequest BuildRequest(string blockHash, object id = null);
#if !DOTNET35
        Task<List<BlockWithTransactions>> SendBatchRequestAsync(params string[] blockHashes);
#endif
        Task<BlockWithTransactions> SendRequestAsync(string blockHash, object id = null);
    }
}