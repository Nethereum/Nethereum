using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.Text;

namespace Nethereum.Wallet
{
    public class BouncyCastleAes256EncryptionStrategy : IEncryptionStrategy
    {
        private const int KeySize = 32;
        private const int IvSize = 16;
        private const int SaltSize = 16;
        private const int Iterations = 10000;

        public byte[] Encrypt(byte[] data, string password)
        {
            var salt = DeriveFixedSalt(password);
            var key = GenerateKey(password, salt);
            
            var iv = new byte[IvSize];
            new SecureRandom().NextBytes(iv);

            var cipher = CreateCipher(true, key, iv);
            
            var encrypted = new byte[cipher.GetOutputSize(data.Length)];
            var len = cipher.ProcessBytes(data, 0, data.Length, encrypted, 0);
            len += cipher.DoFinal(encrypted, len);

            var trimmedEncrypted = new byte[len];
            Array.Copy(encrypted, 0, trimmedEncrypted, 0, len);

            var result = new byte[IvSize + trimmedEncrypted.Length];
            Array.Copy(iv, 0, result, 0, IvSize);
            Array.Copy(trimmedEncrypted, 0, result, IvSize, trimmedEncrypted.Length);

            return result;
        }

        public byte[] Decrypt(byte[] encryptedData, string password)
        {
            if (encryptedData.Length < IvSize)
                throw new ArgumentException("Encrypted data is too short to contain an IV");

            var iv = new byte[IvSize];
            Array.Copy(encryptedData, 0, iv, 0, IvSize);

            var encrypted = new byte[encryptedData.Length - IvSize];
            Array.Copy(encryptedData, IvSize, encrypted, 0, encrypted.Length);

            var salt = DeriveFixedSalt(password);
            var key = GenerateKey(password, salt);

            var cipher = CreateCipher(false, key, iv);

            var decrypted = new byte[cipher.GetOutputSize(encrypted.Length)];
            var len = cipher.ProcessBytes(encrypted, 0, encrypted.Length, decrypted, 0);
            len += cipher.DoFinal(decrypted, len);

            var result = new byte[len];
            Array.Copy(decrypted, 0, result, 0, len);

            return result;
        }

        private byte[] DeriveFixedSalt(string password)
        {
            // This is a simplified approach for wallet encryption
            return Encoding.UTF8.GetBytes("NethereumWallet16");
        }

        private byte[] GenerateKey(string password, byte[] salt)
        {
            var generator = new Pkcs5S2ParametersGenerator();
            generator.Init(Encoding.UTF8.GetBytes(password), salt, Iterations);
            var keyParam = (KeyParameter)generator.GenerateDerivedParameters("AES", KeySize * 8);
            return keyParam.GetKey();
        }

        private PaddedBufferedBlockCipher CreateCipher(bool forEncryption, byte[] key, byte[] iv)
        {
            var aes = new AesEngine();
            var cbc = new CbcBlockCipher(aes);
            var cipher = new PaddedBufferedBlockCipher(cbc);

            var keyParam = new KeyParameter(key);
            var keyParamWithIv = new ParametersWithIV(keyParam, iv);

            cipher.Init(forEncryption, keyParamWithIv);
            return cipher;
        }
    }
}