namespace Nethereum.Merkle.Binary.Storage
{
    public interface IBinaryTrieStorage
    {
        void Put(byte[] key, byte[] value);
        byte[] Get(byte[] key);
        void Delete(byte[] key);
    }
}
