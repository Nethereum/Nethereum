using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Nethereum.Pantheon.RPC.Txpool
{
    /// <Summary>
    ///     Lists transactions in the node transaction pool.
    /// </Summary>
    public class TxpoolPantheonTransactions : GenericRpcRequestResponseHandlerNoParam<JArray>,
        ITxpoolPantheonTransactions
    {
        public TxpoolPantheonTransactions(IClient client) : base(client,
            ApiMethods.txpool_pantheonTransactions.ToString())
        {
        }
    }
}