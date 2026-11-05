using System;

namespace Nethereum.Wallet
{
    [Obsolete("Use Aes256GcmEncryptionStrategy. This class now writes the Aes256GcmEncryptionStrategy format using BouncyCastle only and still reads the legacy AES-256-CBC format.")]
    public class BouncyCastleAes256EncryptionStrategy : IEncryptionStrategy
    {
        private readonly Aes256GcmEncryptionStrategy _current = new Aes256GcmEncryptionStrategy(true);

        public byte[] Encrypt(byte[] data, string password)
        {
            return _current.Encrypt(data, password);
        }

        public byte[] Decrypt(byte[] encryptedData, string password)
        {
            if (!Aes256GcmEncryptionStrategy.IsCurrentFormat(encryptedData)) return LegacyVaultDecryptor.DecryptAesBouncyCastle(encryptedData, password);
            return _current.DecryptCurrentOrLegacy(encryptedData, password, LegacyVaultDecryptor.DecryptAesBouncyCastle);
        }
    }
}