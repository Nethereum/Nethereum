namespace Nethereum.Merkle.Patricia
{
    public interface ITrieStorage
    {
        void Put(byte[] key, byte[] value);
        byte[] Get(byte[] key);
        void Delete(byte[] key);
    }
}
