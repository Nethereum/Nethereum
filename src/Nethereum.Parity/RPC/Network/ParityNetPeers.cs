using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Network
{
    /// <Summary>
    ///     parity_netPeers
    ///     Returns number of peers.
    ///     Parameters
    ///     None
    ///     Returns
    ///     Object - Number of peers
    ///     active: Quantity - Number of active peers.
    ///     connected: Quantity - Number of connected peers.
    ///     max: Quantity - Maximum number of connected peers.
    ///     peers: Array - List of all peers with details.
    ///     Example
    ///     Request
    ///     curl --data '{"method":"parity_netPeers","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type: application/json"
    ///     -X POST localhost:8545
    ///     Response
    ///     {
    ///     "id": 1,
    ///     "jsonrpc": "2.0",
    ///     "result": {
    ///     "active": 0,
    ///     "connected": 25,
    ///     "max": 25,
    ///     "peers": [{ ... }, { ... }, { ... }, ...]
    ///     }
    ///     }
    /// </Summary>
    public class ParityNetPeers : GenericRpcRequestResponseHandlerNoParam<JObject>
    {
        public ParityNetPeers(IClient client) : base(client, ApiMethods.parity_netPeers.ToString())
        {
        }
    }
}