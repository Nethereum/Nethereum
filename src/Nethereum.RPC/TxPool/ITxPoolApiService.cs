namespace Nethereum.RPC.TxPool
{
    public interface ITxPoolApiService
    {
        ITxPoolContent Content { get; }
        ITxPoolContentFrom ContentFrom { get; }
        ITxPoolStatus Status { get; }
    }
}
