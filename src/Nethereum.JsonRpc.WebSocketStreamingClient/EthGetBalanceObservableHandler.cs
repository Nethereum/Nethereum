using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.Streaming;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
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
