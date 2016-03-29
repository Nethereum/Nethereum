using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.RPC.Eth
{
    /// <Summary>
    ///     eth_gasPrice
    ///     Returns the current price per gas in wei.
    ///     Parameters
    ///     none
    ///     Returns
    ///     QUANTITY - integer of the current gas price in wei.
    ///     Example
    ///     Request
    ///     curl -X POST --data '{"jsonrpc":"2.0","method":"eth_gasPrice","params":[],"id":73}'
    ///     Result
    ///     {
    ///     "id":73,
    ///     "jsonrpc": "2.0",
    ///     "result": "0x09184e72a000" // 10000000000000
    ///     }
    /// </Summary>
    public class EthGasPrice : GenericRpcRequestResponseHandlerNoParam<HexBigInteger>
    {
        public EthGasPrice(IClient client) : base(client, ApiMethods.eth_gasPrice.ToString())
        {
        }
    }
}