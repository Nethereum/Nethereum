using Nethereum.JsonRpc.Client;
using Nethereum.Pantheon.RPC.Txpool;
using Nethereum.RPC;

namespace Nethereum.Pantheon
{
    public class TxPoolApiService : RpcClientWrapper, ITxPoolApiService
    {
        public TxPoolApiService(IClient client) : base(client)
        {
            PantheonStatistics = new TxpoolPantheonStatistics(client);
            PantheonTransactions = new TxpoolPantheonTransactions(client);
        }

        public ITxpoolPantheonStatistics PantheonStatistics { get; }
        public ITxpoolPantheonTransactions PantheonTransactions { get; }

    }
}