using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Reactive.RpcStreaming;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.RPC.Reactive.Eth.Blocks
{
    public class EthGetBlockWithTransactionsByHashObservableHandler : RpcStreamingResponseParamsObservableHandler<BlockWithTransactions, EthGetBlockWithTransactionsByHash>
    {
        public EthGetBlockWithTransactionsByHashObservableHandler(IStreamingClient streamingClient) : base(streamingClient, new EthGetBlockWithTransactionsByHash(null))
        {

        }

        public Task SendRequestAsync(string blockHash, object id = null)
        {
            if (id == null) id = Guid.NewGuid().ToString();
            var request = RpcRequestResponseHandler.BuildRequest(blockHash.EnsureHexPrefix(), id);
            return SendRequestAsync(request);
        }
    }
}
