#nullable enable

using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace Nethereum.Wallet
{
    public class Aes256GcmEncryptionStrategy : IEncryptionStrategy
    {
        public const int DefaultIterations = 600000;
        public const int MaxIterations = 10000000;
        public const byte Version = 1;
        public const byte Pbkdf2HmacSha256 = 1;

        private static readonly byte[] Magic = { 0x4E, 0x57, 0x56 };
        private const int VersionOffset = 3;
        private const int KdfOffset = 4;
        private const int IterationsOffset = 5;
        private const int SaltOffset = 9;
        private const int KeySize = 32;
        private const int SaltSize = 16;
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int NonceOffset = SaltOffset + SaltSize;
        private const int HeaderSize = NonceOffset + NonceSize;

        private readonly bool _useBouncyCastle;

        public Aes256GcmEncryptionStrategy() : this(false)
        {
        }

        public Aes256GcmEncryptionStrategy(bool forceBouncyCastle)
        {
            _useBouncyCastle = forceBouncyCastle || !AesGcm.IsSupported;
        }

        public static bool IsCurrentFormat(byte[] data)
        {
            if (data == null || data.Length < HeaderSize + TagSize) return false;
            for (var i = 0; i < Magic.Length; i++)
                if (data[i] != Magic[i]) return false;
            return data[VersionOffset] == Version;
        }

        public byte[] Encrypt(byte[] data, string password)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (password == null) throw new ArgumentNullException(nameof(password));

            var header = new byte[HeaderSize];
            Array.Copy(Magic, 0, header, 0, Magic.Length);
            header[VersionOffset] = Version;
            header[KdfOffset] = Pbkdf2HmacSha256;
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(IterationsOffset), DefaultIterations);

            var salt = new byte[SaltSize];
            var nonce = new byte[NonceSize];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(salt);
                random.GetBytes(nonce);
            }
            Array.Copy(salt, 0, header, SaltOffset, SaltSize);
            Array.Copy(nonce, 0, header, NonceOffset, NonceSize);

            var key = DeriveKey(password, salt, DefaultIterations);
            var sealedData = Seal(key, nonce, header, data);
            var result = new byte[HeaderSize + sealedData.Length];
            Array.Copy(header, 0, result, 0, HeaderSize);
            Array.Copy(sealedData, 0, result, HeaderSize, sealedData.Length);
            return result;
        }

        public byte[] Decrypt(byte[] data, string password)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (password == null) throw new ArgumentNullException(nameof(password));

            if (!IsCurrentFormat(data)) return LegacyVaultDecryptor.Decrypt(data, password);
            return DecryptCurrentOrLegacy(data, password, LegacyVaultDecryptor.Decrypt);
        }

        internal byte[] DecryptCurrentOrLegacy(byte[] data, string password, Func<byte[], string, byte[]> legacy)
        {
            try
            {
                return DecryptCurrent(data, password);
            }
            catch (Exception ex) when (ex is CryptographicException || ex is ArgumentException)
            {
                var fallback = TryLegacy(data, password, legacy);
                if (fallback != null) return fallback;
                throw;
            }
        }

        private static byte[] TryLegacy(byte[] data, string password, Func<byte[], string, byte[]> legacy)
        {
            try
            {
                var result = legacy(data, password);
                return LegacyVaultDecryptor.IsJsonObject(result) ? result : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal byte[] DecryptCurrent(byte[] data, string password)
        {
            if (data[KdfOffset] != Pbkdf2HmacSha256) throw new CryptographicException("Unsupported vault key derivation");
            var iterations = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(IterationsOffset));
            if (iterations < 1 || iterations > MaxIterations)
                throw new CryptographicException("Vault key derivation cost is out of range");

            var header = new byte[HeaderSize];
            Array.Copy(data, 0, header, 0, HeaderSize);
            var salt = new byte[SaltSize];
            Array.Copy(data, SaltOffset, salt, 0, SaltSize);
            var nonce = new byte[NonceSize];
            Array.Copy(data, NonceOffset, nonce, 0, NonceSize);
            var sealedData = new byte[data.Length - HeaderSize];
            Array.Copy(data, HeaderSize, sealedData, 0, sealedData.Length);

            var key = DeriveKey(password, salt, iterations);
            return Open(key, nonce, header, sealedData);
        }

        private byte[] DeriveKey(string password, byte[] salt, int iterations)
        {
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            if (!_useBouncyCastle)
                return Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, iterations, HashAlgorithmName.SHA256, KeySize);

            var generator = new Pkcs5S2ParametersGenerator(new Sha256Digest());
            generator.Init(passwordBytes, salt, iterations);
            return ((KeyParameter)generator.GenerateDerivedMacParameters(KeySize * 8)).GetKey();
        }

        private byte[] Seal(byte[] key, byte[] nonce, byte[] header, byte[] plaintext)
        {
            if (!_useBouncyCastle)
            {
                var result = new byte[plaintext.Length + TagSize];
                var cipherText = new byte[plaintext.Length];
                var tag = new byte[TagSize];
                using (var gcm = new AesGcm(key))
                    gcm.Encrypt(nonce, plaintext, cipherText, tag, header);
                Array.Copy(cipherText, 0, result, 0, cipherText.Length);
                Array.Copy(tag, 0, result, cipherText.Length, TagSize);
                return result;
            }

            var cipher = new GcmBlockCipher(new AesEngine());
            cipher.Init(true, new AeadParameters(new KeyParameter(key), TagSize * 8, nonce, header));
            var buffer = new byte[cipher.GetOutputSize(plaintext.Length)];
            var length = cipher.ProcessBytes(plaintext, 0, plaintext.Length, buffer, 0);
            length += cipher.DoFinal(buffer, length);
            var sealedData = new byte[length];
            Array.Copy(buffer, 0, sealedData, 0, length);
            return sealedData;
        }

        private byte[] Open(byte[] key, byte[] nonce, byte[] header, byte[] sealedData)
        {
            var cipherLength = sealedData.Length - TagSize;
            if (cipherLength < 0) throw new CryptographicException("Vault data is truncated");

            if (!_useBouncyCastle)
            {
                var cipherText = new byte[cipherLength];
                var tag = new byte[TagSize];
                Array.Copy(sealedData, 0, cipherText, 0, cipherLength);
                Array.Copy(sealedData, cipherLength, tag, 0, TagSize);
                var plaintext = new byte[cipherLength];
                using (var gcm = new AesGcm(key))
                    gcm.Decrypt(nonce, cipherText, tag, plaintext, header);
                return plaintext;
            }

            try
            {
                var cipher = new GcmBlockCipher(new AesEngine());
                cipher.Init(false, new AeadParameters(new KeyParameter(key), TagSize * 8, nonce, header));
                var buffer = new byte[cipher.GetOutputSize(sealedData.Length)];
                var length = cipher.ProcessBytes(sealedData, 0, sealedData.Length, buffer, 0);
                length += cipher.DoFinal(buffer, length);
                var result = new byte[length];
                Array.Copy(buffer, 0, result, 0, length);
                return result;
            }
            catch (InvalidCipherTextException)
            {
                throw new CryptographicException("Vault authentication failed");
            }
        }
    }
}
