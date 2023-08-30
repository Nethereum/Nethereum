using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.RPC.Eth.Blocks
{
    public interface IEthGetBlockWithTransactionsByNumber
    {
        RpcRequest BuildRequest(BlockParameter blockParameter, object id = null);
        RpcRequest BuildRequest(HexBigInteger number, object id = null);
        RpcRequestResponseBatchItem<EthGetBlockWithTransactionsByNumber, BlockWithTransactions> CreateBatchItem(HexBigInteger number, object id);

#if !DOTNET35
        Task<List<BlockWithTransactions>> SendBatchRequestAsync(params HexBigInteger[] numbers);
#endif
        Task<BlockWithTransactions> SendRequestAsync(BlockParameter blockParameter, object id = null);
        Task<BlockWithTransactions> SendRequestAsync(HexBigInteger number, object id = null);
    }
}