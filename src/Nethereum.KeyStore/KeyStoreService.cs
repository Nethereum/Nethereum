using System;
using Newtonsoft.Json.Linq;

namespace Nethereum.KeyStore
{
    public class KeyStoreService
    {
        private readonly KeyStoreKdfChecker _keyStoreKdfChecker;
        private readonly KeyStoreScryptService _keyStoreScryptService;
        private readonly KeyStorePbkdf2Service _keyStorePbkdf2Service;

        public KeyStoreService()
        {
            _keyStoreKdfChecker = new KeyStoreKdfChecker();
            _keyStorePbkdf2Service = new KeyStorePbkdf2Service();
            _keyStoreScryptService = new KeyStoreScryptService();
        }

        public KeyStoreService(KeyStoreKdfChecker keyStoreKdfChecker, KeyStoreScryptService keyStoreScryptService, KeyStorePbkdf2Service keyStorePbkdf2Service)
        {
            _keyStoreKdfChecker = keyStoreKdfChecker;
            _keyStoreScryptService = keyStoreScryptService;
            _keyStorePbkdf2Service = keyStorePbkdf2Service;
        }

        public string GetAddressFromKeyStore(string json)
        {
            var keyStoreDocument = JObject.Parse(json);
            return keyStoreDocument["address"].Value<string>();
        }

        public byte[] DecryptKeyStoreFromJson(string password, string json)
        {
            var type = _keyStoreKdfChecker.GetKeyStoreKdfType(json);
            if (type == KeyStoreKdfChecker.KdfType.pbkdf2)
            {
                return _keyStorePbkdf2Service.DecryptKeyStoreFromJson(password, json);
            }

            if (type == KeyStoreKdfChecker.KdfType.scrypt)
            {
                return _keyStoreScryptService.DecryptKeyStoreFromJson(password, json);
            }
            //shold not reach here, already handled by the checker
            throw new Exception("Invalid kdf type");
        }

        public string EncryptAndGenerateDefaultKeyStoreAsJson(string password, byte[] key, string address)
        {
            return _keyStoreScryptService.EncryptAndGenerateKeyStoreAsJson(password, key, address);
        }
    }
}