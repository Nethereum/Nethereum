using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.RPC.Eth.Mining
{
    /// <Summary>
    ///     eth_mining
    ///     Returns true if client is actively mining new blocks.
    ///     Parameters
    ///     none
    ///     Returns
    ///     Boolean - returns true of the client is mining, otherwise false.
    ///     Example
    ///     Request
    ///     curl -X POST --data '{"jsonrpc":"2.0","method":"eth_mining","params":[],"id":71}'
    ///     Result
    ///     {
    ///     "id":71,
    ///     "jsonrpc": "2.0",
    ///     "result": true
    ///     }
    /// </Summary>
    public class EthMining : GenericRpcRequestResponseHandlerNoParam<bool>
    {
        public EthMining(IClient client) : base(client, ApiMethods.eth_mining.ToString())
        {
        }
    }
}