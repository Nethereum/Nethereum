using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.RPC.Eth.Mining
{
    /// <Summary>
    ///     eth_hashrate
    ///     Returns the number of hashes per second that the node is mining with.
    ///     Parameters
    ///     none
    ///     Returns
    ///     QUANTITY - number of hashes per second.
    ///     Example
    ///     Request
    ///     curl -X POST --data '{"jsonrpc":"2.0","method":"eth_hashrate","params":[],"id":71}'
    ///     Result
    ///     {
    ///     "id":71,
    ///     "jsonrpc": "2.0",
    ///     "result": "0x38a"
    ///     }
    /// </Summary>
    public class EthHashrate : GenericRpcRequestResponseHandlerNoParam<HexBigInteger>, IEthHashrate
    {
        public EthHashrate(IClient client) : base(client, ApiMethods.eth_hashrate.ToString())
        {
        }
    }

   
}