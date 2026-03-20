namespace Nethereum.Merkle.Sparse
{
    public interface ISmtHasher
    {
        bool MsbFirst { get; }
        bool UseFixedEmptyHash { get; }
        bool CollapseSingleLeaf { get; }
        byte[] EmptyLeaf { get; }
        byte[] HashLeaf(byte[] path, byte[] valueBytes);
        byte[] HashNode(byte[] leftHash, byte[] rightHash);
    }
}
