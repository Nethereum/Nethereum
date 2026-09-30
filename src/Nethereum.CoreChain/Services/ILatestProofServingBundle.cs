namespace Nethereum.CoreChain.Services
{
    public interface ILatestProofServingBundle
    {
        Nethereum.Merkle.Patricia.Storage.ITrieNodeStore LatestProofNodeStore { get; }
    }
}
