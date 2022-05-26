using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Reactive.RpcStreaming;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.RPC.Reactive.Eth.Transactions
{
    public class EthGetTransactionByHashObservableHandler : RpcStreamingResponseParamsObservableHandler<Transaction, EthGetTransactionByHash>
    {
        public EthGetTransactionByHashObservableHandler(IStreamingClient streamingClient) : base(streamingClient, new EthGetTransactionByHash(null))
        {

        }

        public Task SendRequestAsync(string hashTransaction, object id = null)
        {
            if (id == null) id = Guid.NewGuid().ToString();
            var request = RpcRequestResponseHandler.BuildRequest(hashTransaction.EnsureHexPrefix(), id);
            return SendRequestAsync(request);
        }
    }
}
