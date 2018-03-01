using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Parity.RPC.BlockAuthoring
{
    /// <Summary>
    ///     parity_minGasPrice
    ///     Returns currently set minimal gas price
    ///     Parameters
    ///     None
    ///     Returns
    ///     Quantity - Minimal Gas Price
    ///     Example
    ///     Request
    ///     curl --data '{"method":"parity_minGasPrice","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type:
    ///     application/json" -X POST localhost:8545
    ///     Response
    ///     {
    ///     "id": 1,
    ///     "jsonrpc": "2.0",
    ///     "result": "0x29f507000" // 11262783488
    ///     }
    /// </Summary>
    public class ParityMinGasPrice : GenericRpcRequestResponseHandlerNoParam<HexBigInteger>
    {
        public ParityMinGasPrice(IClient client) : base(client, ApiMethods.parity_minGasPrice.ToString())
        {
        }
    }
}