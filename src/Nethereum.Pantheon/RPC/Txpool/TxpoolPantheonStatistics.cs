using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Pantheon.RPC.Txpool
{
    /// <Summary>
    ///     Lists statistics about the node transaction pool.
    /// </Summary>
    public class TxpoolPantheonStatistics : GenericRpcRequestResponseHandlerNoParam<JObject>, ITxpoolPantheonStatistics
    {
        public TxpoolPantheonStatistics(IClient client) : base(client, ApiMethods.txpool_pantheonStatistics.ToString())
        {
        }
    }
}