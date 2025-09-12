#nullable enable

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Nethereum.Wallet
{
    public class DefaultAes256EncryptionStrategy : IEncryptionStrategy
    {
        private const int Iterations = 10000;
        private const int KeySize = 32;
        private const int IvSize = 16;

        public byte[] Encrypt(byte[] data, string password)
        {
            using var aes = Aes.Create();
            aes.KeySize = KeySize * 8;
            aes.BlockSize = IvSize * 8;
            aes.GenerateIV();
            var salt = Encoding.UTF8.GetBytes("NethereumWallet16");
            aes.Key = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256).GetBytes(KeySize);

            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            ms.Write(aes.IV, 0, aes.IV.Length);
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            {
                cs.Write(data, 0, data.Length);
                cs.FlushFinalBlock();
            }
            return ms.ToArray();
        }

        public byte[] Decrypt(byte[] data, string password)
        {
            var iv = new byte[IvSize];
            Array.Copy(data, 0, iv, 0, IvSize);

            using var aes = Aes.Create();
            aes.KeySize = KeySize * 8;
            aes.BlockSize = IvSize * 8;
            aes.IV = iv;
            var salt = Encoding.UTF8.GetBytes("NethereumWallet16");
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

    }
}
