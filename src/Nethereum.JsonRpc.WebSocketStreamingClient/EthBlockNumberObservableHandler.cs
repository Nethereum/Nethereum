using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.RPC.Eth.Blocks;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
{
    public class EthBlockNumberObservableHandler : RpcStreamingResponseNoParamsObservableHandler<HexBigInteger, EthBlockNumber>
    {
        public EthBlockNumberObservableHandler(IStreamingClient streamingClient) : base(streamingClient, new EthBlockNumber(null))
        {

        }
    }
}
