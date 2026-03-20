namespace Nethereum.Merkle.Binary.Nodes
{
    public interface IBinaryNode
    {
        byte[] Get(byte[] key, NodeResolverFunc resolver);
        IBinaryNode Insert(byte[] key, byte[] value, NodeResolverFunc resolver, int depth);
        byte[][] GetValuesAtStem(byte[] stem, NodeResolverFunc resolver);
        IBinaryNode InsertValuesAtStem(byte[] stem, byte[][] values, NodeResolverFunc resolver, int depth);
        byte[] ComputeHash(Nethereum.Util.HashProviders.IHashProvider hashProvider);
        IBinaryNode Copy();
        int GetHeight();
    }
}
