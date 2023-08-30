using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Transactions
{
    public interface IEthGetTransactionByHash
    {
        RpcRequest BuildRequest(string hashTransaction, object id = null);
        RpcRequestResponseBatchItem<EthGetTransactionByHash, Transaction> CreateBatchItem(string transactionHash, object id);
#if !DOTNET35
        Task<List<Transaction>> SendBatchRequestAsync(string[] transactionHashes);
#endif
        Task<Transaction> SendRequestAsync(string hashTransaction, object id = null);
    }
}