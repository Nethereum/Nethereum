#nullable enable

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Nethereum.KeyStore;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;

namespace Nethereum.Wallet
{
    internal static class LegacyVaultDecryptor
    {
        private const int Iterations = 10000;
        private const int KeySize = 32;
        private const int IvSize = 16;
        private const string FixedSalt = "NethereumWallet16";

        public static byte[] Decrypt(byte[] data, string password)
        {
            if (IsKeyStoreFormat(data)) return DecryptKeyStore(data, password);

            foreach (var variant in new Func<byte[], string, byte[]>[] { DecryptAesSha256, DecryptAesBouncyCastle })
            {
                try
                {
                    var candidate = variant(data, password);
                    if (IsJsonObject(candidate)) return candidate;
                }
                catch (CryptographicException)
                {
                }
                catch (ArgumentException)
                {
                }
                catch (Org.BouncyCastle.Crypto.CryptoException)
                {
                }
            }

            throw new CryptographicException("Vault decryption failed");
        }

        public static bool IsKeyStoreFormat(byte[] data)
        {
            if (data == null) return false;
            var first = 0;
            while (first < data.Length && (data[first] == 0x20 || data[first] == 0x09 || data[first] == 0x0D || data[first] == 0x0A)) first++;
            if (first == data.Length || data[first] != (byte)'{') return false;
            try
            {
                using var document = JsonDocument.Parse(data);
                return document.RootElement.ValueKind == JsonValueKind.Object;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static byte[] DecryptKeyStore(byte[] data, string password)
        {
            return new KeyStoreService().DecryptPayloadFromJson(password, Encoding.UTF8.GetString(data));
        }

        public static byte[] DecryptAesSha256(byte[] data, string password)
        {
            if (data.Length < IvSize) throw new ArgumentException("Encrypted data is too short to contain an IV");
            var iv = new byte[IvSize];
            Array.Copy(data, 0, iv, 0, IvSize);

            using var aes = Aes.Create();
            aes.KeySize = KeySize * 8;
            aes.BlockSize = IvSize * 8;
            aes.IV = iv;
            var salt = Encoding.UTF8.GetBytes(FixedSalt);
            aes.Key = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256).GetBytes(KeySize);

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
            {
                cs.Write(data, IvSize, data.Length - IvSize);
                cs.FlushFinalBlock();
            }
            return ms.ToArray();
        }

        public static byte[] DecryptAesBouncyCastle(byte[] encryptedData, string password)
        {
            if (encryptedData.Length < IvSize)
                throw new ArgumentException("Encrypted data is too short to contain an IV");

            var iv = new byte[IvSize];
            Array.Copy(encryptedData, 0, iv, 0, IvSize);

            var encrypted = new byte[encryptedData.Length - IvSize];
            Array.Copy(encryptedData, IvSize, encrypted, 0, encrypted.Length);

            var generator = new Pkcs5S2ParametersGenerator();
            generator.Init(Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(FixedSalt), Iterations);
            var key = ((KeyParameter)generator.GenerateDerivedParameters("AES", KeySize * 8)).GetKey();

            var cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(new AesEngine()));
            cipher.Init(false, new ParametersWithIV(new KeyParameter(key), iv));

            var decrypted = new byte[cipher.GetOutputSize(encrypted.Length)];
            var len = cipher.ProcessBytes(encrypted, 0, encrypted.Length, decrypted, 0);
            len += cipher.DoFinal(decrypted, len);

            var result = new byte[len];
            Array.Copy(decrypted, 0, result, 0, len);
            return result;
        }

        internal static bool IsJsonObject(byte[] plaintext)
        {
            try
            {
                new UTF8Encoding(false, true).GetString(plaintext);
                using var document = JsonDocument.Parse(plaintext);
                return document.RootElement.ValueKind == JsonValueKind.Object;
            }
            catch (JsonException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
