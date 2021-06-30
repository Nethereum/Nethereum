using Nethereum.JsonRpc.Client;
using Nethereum.Besu.RPC.Txpool;
using Nethereum.RPC;

namespace Nethereum.Besu
{
    public class TxPoolApiService : RpcClientWrapper, ITxPoolApiService
    {
        public TxPoolApiService(IClient client) : base(client)
        {
            BesuStatistics = new TxpoolBesuStatistics(client);
            BesuTransactions = new TxpoolBesuTransactions(client);
        }

        public ITxpoolBesuStatistics BesuStatistics { get; }
        public ITxpoolBesuTransactions BesuTransactions { get; }

    }
}