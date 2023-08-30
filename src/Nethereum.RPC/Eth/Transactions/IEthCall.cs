using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Transactions
{
    public interface IEthCall
    {
        BlockParameter DefaultBlock { get; set; }

        RpcRequest BuildRequest(CallInput callInput, BlockParameter block, object id = null);
        RpcRequestResponseBatchItem<EthCall, string> CreateBatchItem(CallInput callInput, BlockParameter block, object id);
        RpcRequestResponseBatchItem<EthCall, string> CreateBatchItem(CallInput callInput, object id);
#if !DOTNET35
        Task<List<string>> SendBatchRequestAsync(params CallInput[] callInputs);
        Task<List<string>> SendBatchRequestAsync(CallInput[] callInputs, BlockParameter block);
#endif
        Task<string> SendRequestAsync(CallInput callInput, object id = null);
        Task<string> SendRequestAsync(CallInput callInput, BlockParameter block, object id = null);
    }
}