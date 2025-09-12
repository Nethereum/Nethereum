#nullable enable

namespace Nethereum.Wallet
{
    public interface IEncryptionStrategy
    {
        byte[] Encrypt(byte[] data, string password);
        byte[] Decrypt(byte[] data, string password);
    }
}
