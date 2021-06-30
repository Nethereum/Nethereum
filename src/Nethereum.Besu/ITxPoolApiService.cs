using Nethereum.Besu.RPC.Txpool;

namespace Nethereum.Besu
{
    public interface ITxPoolApiService
    {
        ITxpoolBesuStatistics BesuStatistics { get; }
        ITxpoolBesuTransactions BesuTransactions { get; }
    }
}