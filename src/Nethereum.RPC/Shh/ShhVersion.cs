using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.RPC.Shh
{
    /// <Summary>
    ///     shh_version
    ///     Returns the current whisper protocol version.
    ///     Parameters
    ///     none
    ///     Returns
    ///     String - The current whisper protocol version
    ///     Example
    ///     Request
    ///     curl -X POST --data '{"jsonrpc":"2.0","method":"shh_version","params":[],"id":67}'
    ///     Result
    ///     {
    ///     "id":67,
    ///     "jsonrpc": "2.0",
    ///     "result": "2"
    ///     }
    /// </Summary>
    public class ShhVersion : GenericRpcRequestResponseHandlerNoParam<string>, IShhVersion
    {
        public ShhVersion(IClient client) : base(client, ApiMethods.shh_version.ToString())
        {
        }
    }
}