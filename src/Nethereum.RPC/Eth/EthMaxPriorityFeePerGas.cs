using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.RPC.Eth
{
    /// <summary>
    /// Returns the current maxPriorityFeePerGas per gas in wei.
    /// </summary>
    public class EthMaxPriorityFeePerGas: GenericRpcRequestResponseHandlerNoParam<HexBigInteger>
    {
        public EthMaxPriorityFeePerGas(IClient client) : base(client, ApiMethods.eth_maxPriorityFeePerGas.ToString())
        {
        }
    }
}