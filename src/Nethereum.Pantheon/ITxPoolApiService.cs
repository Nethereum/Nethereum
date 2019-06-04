using Nethereum.Pantheon.RPC.Txpool;

namespace Nethereum.Pantheon
{
    public interface ITxPoolApiService
    {
        ITxpoolPantheonStatistics PantheonStatistics { get; }
        ITxpoolPantheonTransactions PantheonTransactions { get; }
    }
}