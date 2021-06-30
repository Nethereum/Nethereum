using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Besu.RPC.Txpool
{
    /// <Summary>
    ///     Lists transactions in the node transaction pool.
    /// </Summary>
    public class TxpoolBesuTransactions : GenericRpcRequestResponseHandlerNoParam<JArray>,
        ITxpoolBesuTransactions
    {
        public TxpoolBesuTransactions(IClient client) : base(client,
            ApiMethods.txpool_BesuTransactions.ToString())
        {
        }
    }
}