namespace Nethereum.CoreChain.Validation
{
    public enum DivergenceOutcome
    {
        EvmBug,

        PeerLied,

        SourceUnavailable
    }

    public sealed record DivergenceVerdict(
        DivergenceOutcome Outcome,
        byte[] CanonicalStateRoot,
        byte[] CanonicalBlockHash,
        string SourceName,
        string Detail);
}
