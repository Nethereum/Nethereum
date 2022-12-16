namespace Nethereum.Merkle.HashProviders
{
    public interface IHashProvider
    {
        byte[] ComputeHash(byte[] data);
    }

}
