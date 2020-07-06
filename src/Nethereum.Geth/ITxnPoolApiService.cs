using Nethereum.Geth.RPC.TxnPool;

namespace Nethereum.Geth
{
    public interface ITxnPoolApiService
    {
        ITxnPoolContent PoolContent { get; }
        ITxnPoolInspect PoolInspect { get; }
        ITxnPoolStatus PoolStatus { get; }
    }
}