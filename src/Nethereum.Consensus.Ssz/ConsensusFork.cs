namespace Nethereum.Consensus.Ssz
{
    /// <summary>
    /// Consensus-layer hard forks affecting LightClient* SSZ container shapes,
    /// merkleization indices, and proof verification depths.
    /// Ordering is chronological — comparisons like <c>fork &gt;= Capella</c> are spec-meaningful.
    /// </summary>
    public enum ConsensusFork
    {
        Altair = 0,
        Bellatrix = 1,
        Capella = 2,
        Deneb = 3,
        Electra = 4,
        Fulu = 5,
        Gloas = 6
    }
}
