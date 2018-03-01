using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;

namespace Nethereum.Parity.RPC.Development
{
    /// <Summary>
    ///     parity_devLogs
    ///     Returns latest stdout logs of your node.
    ///     Parameters
    ///     None
    ///     Returns
    ///     Array - Development logs
    ///     Example
    ///     Request
    ///     curl --data '{"method":"parity_devLogs","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type: application/json" -X
    ///     POST localhost:8545
    ///     Response
    ///     {
    ///     "id": 1,
    ///     "jsonrpc": "2.0",
    ///     "result": [
    ///     "2017-01-20 18:14:19  Updated conversion rate to Ξ1 = US$10.63 (11199212000 wei/gas)",
    ///     "2017-01-20 18:14:19  Configured for DevelopmentChain using InstantSeal engine",
    ///     "2017-01-20 18:14:19  Operating mode: active",
    ///     "2017-01-20 18:14:19  State DB configuration: fast",
    ///     "2017-01-20 18:14:19  Starting Parity/v1.6.0-unstable-2ae8b4c-20170120/x86_64-linux-gnu/rustc1.14.0"
    ///     ]
    ///     }
    /// </Summary>
    public class ParityDevLogs : GenericRpcRequestResponseHandlerNoParam<string[]>
    {
        public ParityDevLogs(IClient client) : base(client, ApiMethods.parity_devLogs.ToString())
        {
        }
    }
}