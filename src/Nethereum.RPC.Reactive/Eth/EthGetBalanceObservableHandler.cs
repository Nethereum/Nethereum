using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.RpcStreaming;

namespace Nethereum.RPC.Reactive.Eth
{
    public class EthGetBalanceObservableHandler : RpcStreamingResponseParamsObservableHandler<HexBigInteger, EthGetBalance>
    {
        public EthGetBalanceObservableHandler(IStreamingClient streamingClient) : base(streamingClient, new EthGetBalance(null))
        {

        }

        public Task SendRequestAsync(string address, BlockParameter block, object id = null)
        {
            if (id == null) id = Guid.NewGuid().ToString();
            var request = RpcRequestResponseHandler.BuildRequest(address, block, id);
            return SendRequestAsync(request);
        }
    }
}
