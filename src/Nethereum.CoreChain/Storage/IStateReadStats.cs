namespace Nethereum.CoreChain.Storage
{
    public interface IStateReadStats
    {
        long AccountReads { get; }
        long StorageReads { get; }
    }
}
