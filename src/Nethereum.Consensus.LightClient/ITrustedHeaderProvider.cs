namespace Nethereum.Consensus.LightClient
{
    public interface ITrustedHeaderProvider
    {
        string Name => GetType().Name;

        TrustedExecutionHeader GetLatestFinalized();
        TrustedExecutionHeader GetLatestOptimistic();
        byte[] GetBlockHash(ulong blockNumber);
    }
}
