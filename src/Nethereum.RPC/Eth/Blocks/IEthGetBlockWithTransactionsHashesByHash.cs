using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Blocks
{
    public interface IEthGetBlockWithTransactionsHashesByHash
    {
        RpcRequest BuildRequest(string blockHash, object id = null);
        RpcRequestResponseBatchItem<EthGetBlockWithTransactionsHashesByHash, BlockWithTransactionHashes> CreateBatchItem(string blockHash, object id);
#if !DOTNET35
        Task<List<BlockWithTransactionHashes>> SendBatchRequestAsync(params string[] blockHashes);
#endif
        Task<BlockWithTransactionHashes> SendRequestAsync(string blockHash, object id = null);
    }
}