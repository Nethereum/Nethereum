using System;
using System.IO;
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

        public KeyStoreService(KeyStoreKdfChecker keyStoreKdfChecker, KeyStoreScryptService keyStoreScryptService,
            KeyStorePbkdf2Service keyStorePbkdf2Service)
        {
            _keyStoreKdfChecker = keyStoreKdfChecker;
            _keyStoreScryptService = keyStoreScryptService;
            _keyStorePbkdf2Service = keyStorePbkdf2Service;
        }

        public string GetAddressFromKeyStore(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var keyStoreDocument = JObject.Parse(json);
            return keyStoreDocument["address"].Value<string>();
        }

        public string GenerateUTCFileName(string address)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            return "UTC--" + DateTime.UtcNow.ToString("O").Replace(":", "-") + "--" + address.Replace("0x", "");
        }
#if !PCL 
        public byte[] DecryptKeyStoreFromFile(string password, string filePath)
        {
            using (var file = File.OpenText(filePath))
            {
                var json = file.ReadToEnd();
                return DecryptKeyStoreFromJson(password, json);
            }
        }
#endif

        public byte[] DecryptKeyStoreFromJson(string password, string json)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (json == null) throw new ArgumentNullException(nameof(json));

            var type = _keyStoreKdfChecker.GetKeyStoreKdfType(json);
            if (type == KeyStoreKdfChecker.KdfType.pbkdf2)
                return _keyStorePbkdf2Service.DecryptKeyStoreFromJson(password, json);

            if (type == KeyStoreKdfChecker.KdfType.scrypt)
                return _keyStoreScryptService.DecryptKeyStoreFromJson(password, json);
            //shold not reach here, already handled by the checker
            throw new Exception("Invalid kdf type");
        }

        public string EncryptAndGenerateDefaultKeyStoreAsJson(string password, byte[] key, string address)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (address == null) throw new ArgumentNullException(nameof(address));

            return _keyStoreScryptService.EncryptAndGenerateKeyStoreAsJson(password, key, address);
        }
    }
}