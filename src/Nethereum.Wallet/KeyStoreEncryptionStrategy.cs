#nullable enable

using System;
using System.Text;
using Nethereum.KeyStore;

namespace Nethereum.Wallet
{
    public class KeyStoreEncryptionStrategy : IEncryptionStrategy
    {
        private readonly KeyStoreService _keyStoreService;

        public KeyStoreEncryptionStrategy()
        {
            _keyStoreService = new KeyStoreService();
        }

        public KeyStoreEncryptionStrategy(KeyStoreService keyStoreService)
        {
            _keyStoreService = keyStoreService ?? throw new ArgumentNullException(nameof(keyStoreService));
        }

        public byte[] Encrypt(byte[] data, string password)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (password == null) throw new ArgumentNullException(nameof(password));

            var json = _keyStoreService.EncryptPayloadAsJson(password, data);
            return Encoding.UTF8.GetBytes(json);
        }

        public byte[] Decrypt(byte[] data, string password)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (password == null) throw new ArgumentNullException(nameof(password));

            var json = Encoding.UTF8.GetString(data);
            return _keyStoreService.DecryptPayloadFromJson(password, json);
        }
    }
}
