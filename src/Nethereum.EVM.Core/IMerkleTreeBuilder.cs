namespace Nethereum.EVM
{
    public interface IMerkleTreeBuilder
    {
        void Put(byte[] key, byte[] value);
        byte[] ComputeRoot();
    }
}
