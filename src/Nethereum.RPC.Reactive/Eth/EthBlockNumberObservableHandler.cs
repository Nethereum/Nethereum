using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.RPC.Reactive.RpcStreaming;

namespace Nethereum.RPC.Reactive.Eth
{
    public class EthBlockNumberObservableHandler : RpcStreamingResponseNoParamsObservableHandler<HexBigInteger, EthBlockNumber>
    {
        public EthBlockNumberObservableHandler(IStreamingClient streamingClient) : base(streamingClient, new EthBlockNumber(null))
        {

        }
    }
}
