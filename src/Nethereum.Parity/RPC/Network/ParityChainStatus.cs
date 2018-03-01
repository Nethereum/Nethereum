using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Parity.RPC.Network
{
    /// <Summary>
    ///     parity_chainStatus
    ///     Returns the information on warp sync blocks
    ///     Parameters
    ///     None
    ///     Returns
    ///     Object - The status object
    ///     blockGap: Array - (optional) Describes the gap in the blockchain, if there is one: (first, last)
    ///     Example
    ///     Request
    ///     curl --data '{"method":"parity_chainStatus","params":[],"id":1,"jsonrpc":"2.0"}' -H "Content-Type:
    ///     application/json" -X POST localhost:8545
    ///     Response
    ///     {
    ///     "id": 1,
    ///     "jsonrpc": "2.0",
    ///     "result": {
    ///     "blockGap": undefined
    ///     }
    ///     }
    /// </Summary>
    public class ParityChainStatus : GenericRpcRequestResponseHandlerNoParam<JObject>
    {
        public ParityChainStatus(IClient client) : base(client, ApiMethods.parity_chainStatus.ToString())
        {
        }
    }
}