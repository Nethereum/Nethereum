using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;

namespace Nethereum.RPC.Eth.Transactions
{
    public interface IEthSendRawTransaction
    {
        RpcRequest BuildRequest(string signedTransactionData, object id = null);
        RpcRequestResponseBatchItem<EthSendRawTransaction, string> CreateBatchItem(string signedTransactionData, object id = null);
#if !DOTNET35
        Task<List<string>> SendBatchRequestAsync(params string[] signedTransactionDatas);
#endif
        Task<string> SendRequestAsync(string signedTransactionData, object id = null);
    }
}