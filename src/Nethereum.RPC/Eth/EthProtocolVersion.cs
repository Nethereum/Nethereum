using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.RPC.Eth
{
    /// <Summary>
    ///     eth_protocolVersion
    ///     Returns the current ethereum protocol version.
    ///     Parameters
    ///     none
    ///     Returns
    ///     String - The current ethereum protocol version
    ///     Example
    ///     Request
    ///     curl -X POST --data '{"jsonrpc":"2.0","method":"eth_protocolVersion","params":[],"id":67}'
    ///     Result
    ///     {
    ///     "id":67,
    ///     "jsonrpc": "2.0",
    ///     "result": "54"
    ///     }
    /// </Summary>
    public class EthProtocolVersion : GenericRpcRequestResponseHandlerNoParam<string>
    {
        public EthProtocolVersion(IClient client) : base(client, ApiMethods.eth_protocolVersion.ToString())
        {
        }
    }
}