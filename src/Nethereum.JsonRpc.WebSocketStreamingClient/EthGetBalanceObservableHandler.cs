using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;
using System;
using System.Threading.Tasks;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
{
    public class EthGetBalanceObservableHandler : RpcStreamingRequestResponseParamsObservableHandler<HexBigInteger, EthGetBalance>
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
