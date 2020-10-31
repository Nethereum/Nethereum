using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Geth.RPC.TxnPool
{
    /// <summary>
    /// The status inspection property can be queried for the number of transactions currently pending for inclusion in the next block(s), as well as the ones that are being scheduled for future execution only.
    ///
    /// The result is an object with two fields pending and queued, each of which is a counter representing the number of transactions in that particular state.
    /// </summary>
    public class TxnPoolStatus : GenericRpcRequestResponseHandlerNoParam<JObject>, ITxnPoolStatus
    {
        public TxnPoolStatus(IClient client) : base(client, ApiMethods.txpool_status.ToString())
        {
        }
    }
}