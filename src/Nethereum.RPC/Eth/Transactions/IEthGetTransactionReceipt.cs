using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Transactions
{
    public interface IEthGetTransactionReceipt
    {
        RpcRequest BuildRequest(string transactionHash, object id = null);
        RpcRequestResponseBatchItem<EthGetTransactionReceipt, TransactionReceipt> CreateBatchItem(string transactionHash, object id);
#if !DOTNET35
        Task<List<TransactionReceipt>> SendBatchRequestAsync(string[] transactionHashes);
#endif
        Task<TransactionReceipt> SendRequestAsync(string transactionHash, object id = null);
    }
}