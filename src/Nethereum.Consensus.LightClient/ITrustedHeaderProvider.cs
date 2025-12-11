namespace Nethereum.Consensus.LightClient
{
    public interface ITrustedHeaderProvider
    {
        TrustedExecutionHeader GetLatestFinalized();
        TrustedExecutionHeader GetLatestOptimistic();
        byte[] GetBlockHash(ulong blockNumber);
    }
}
