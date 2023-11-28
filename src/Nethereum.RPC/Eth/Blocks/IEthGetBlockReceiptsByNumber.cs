using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.RPC.Eth.Blocks
{
    public interface IEthGetBlockReceiptsByNumber
    {
        RpcRequest BuildRequest(BlockParameter blockParameter, object id = null);
        RpcRequest BuildRequest(HexBigInteger number, object id = null);
        RpcRequestResponseBatchItem<EthGetBlockReceiptsByNumber, TransactionReceipt[]> CreateBatchItem(HexBigInteger number, object id);
#if !DOTNET35
        Task<List<TransactionReceipt[]>> SendBatchRequestAsync(params HexBigInteger[] numbers);
#endif
        Task<TransactionReceipt[]> SendRequestAsync(BlockParameter blockParameter, object id = null);
        Task<TransactionReceipt[]> SendRequestAsync(HexBigInteger number, object id = null);
    }
}