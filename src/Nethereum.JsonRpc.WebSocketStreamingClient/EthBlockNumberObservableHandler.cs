using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.Blocks;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
{
    public class EthBlockNumberObservableHandler : RpcStreamingRequestResponseNoParamsObservableHandler<HexBigInteger, EthBlockNumber>
    {
        public EthBlockNumberObservableHandler(IStreamingClient streamingClient) : base(streamingClient, new EthBlockNumber(null))
        {
        }
    }
}
