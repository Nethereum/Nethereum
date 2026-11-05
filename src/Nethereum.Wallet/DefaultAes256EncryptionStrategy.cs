#nullable enable

using System;

namespace Nethereum.Wallet
{
    [Obsolete("Use Aes256GcmEncryptionStrategy. This class now writes the Aes256GcmEncryptionStrategy format and still reads the legacy AES-256-CBC format.")]
    public class DefaultAes256EncryptionStrategy : IEncryptionStrategy
    {
        private readonly Aes256GcmEncryptionStrategy _current = new Aes256GcmEncryptionStrategy();

        public byte[] Encrypt(byte[] data, string password)
        {
            return _current.Encrypt(data, password);
        }

        public byte[] Decrypt(byte[] data, string password)
        {
            if (!Aes256GcmEncryptionStrategy.IsCurrentFormat(data)) return LegacyVaultDecryptor.DecryptAesSha256(data, password);
            return _current.DecryptCurrentOrLegacy(data, password, LegacyVaultDecryptor.DecryptAesSha256);
        }
    }
}
