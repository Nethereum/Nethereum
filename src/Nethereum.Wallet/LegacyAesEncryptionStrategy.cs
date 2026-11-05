#nullable enable

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;

namespace Nethereum.Wallet
{
    internal sealed class LegacyAesEncryptionStrategy : IEncryptionStrategy
    {
        private const int Iterations = 10000;
        private const int KeySize = 32;
        private const int IvSize = 16;
        private const string FixedSalt = "NethereumWallet16";

        private readonly bool _bouncyCastleSha1;

        public LegacyAesEncryptionStrategy(bool bouncyCastleSha1)
        {
            _bouncyCastleSha1 = bouncyCastleSha1;
        }

        public byte[] Encrypt(byte[] data, string password)
        {
            var iv = new byte[IvSize];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(iv);
            var salt = Encoding.UTF8.GetBytes(FixedSalt);

            byte[] key;
            if (_bouncyCastleSha1)
            {
                var generator = new Pkcs5S2ParametersGenerator();
                generator.Init(Encoding.UTF8.GetBytes(password), salt, Iterations);
                key = ((KeyParameter)generator.GenerateDerivedParameters("AES", KeySize * 8)).GetKey();
            }
            else
            {
                key = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256).GetBytes(KeySize);
            }

            var cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(new AesEngine()));
            cipher.Init(true, new ParametersWithIV(new KeyParameter(key), iv));
            var buffer = new byte[cipher.GetOutputSize(data.Length)];
            var length = cipher.ProcessBytes(data, 0, data.Length, buffer, 0);
            length += cipher.DoFinal(buffer, length);

            var result = new byte[IvSize + length];
            Array.Copy(iv, 0, result, 0, IvSize);
            Array.Copy(buffer, 0, result, IvSize, length);
            return result;
        }

        public byte[] Decrypt(byte[] data, string password)
        {
            return _bouncyCastleSha1
                ? LegacyVaultDecryptor.DecryptAesBouncyCastle(data, password)
                : LegacyVaultDecryptor.DecryptAesSha256(data, password);
        }
    }
}
