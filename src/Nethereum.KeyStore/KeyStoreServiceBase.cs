using System;
using System.Text;
using Nethereum.KeyStore.Crypto;
using Nethereum.KeyStore.JsonDeserialisation;
using Nethereum.KeyStore.Model;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Utilities;

namespace Nethereum.KeyStore
{
    public abstract class KeyStoreServiceBase<T> : IKeyStoreService<T> where T : KdfParams
    {
        public const int CurrentVersion = 3;
        protected readonly KeyStoreCrypto KeyStoreCrypto;
        protected readonly IRandomBytesGenerator RandomBytesGenerator;

        protected KeyStoreServiceBase() : this(new RandomBytesGenerator(), new KeyStoreCrypto())
        {
        }

        protected KeyStoreServiceBase(IRandomBytesGenerator randomBytesGenerator, KeyStoreCrypto keyStoreCrypto)
        {
            RandomBytesGenerator = randomBytesGenerator;
            KeyStoreCrypto = keyStoreCrypto;
        }


        protected KeyStoreServiceBase(IRandomBytesGenerator randomBytesGenerator)
        {
            RandomBytesGenerator = randomBytesGenerator;
            KeyStoreCrypto = new KeyStoreCrypto();
        }

        public KeyStore<T> EncryptAndGenerateKeyStore(string password, byte[] privateKey, string address)
        {
            var kdfParams = GetDefaultParams();
            return EncryptAndGenerateKeyStore(password, privateKey, address, kdfParams);
        }

        public string EncryptAndGenerateKeyStoreAsJson(string password, byte[] privateKey, string addresss)
        {
            var keyStore = EncryptAndGenerateKeyStore(password, privateKey, addresss);
            return SerializeKeyStoreToJson(keyStore);
        }

        public CryptoStore<T> EncryptAndGenerateCryptoStore(string password, byte[] payload)
        {
            var kdfParams = GetDefaultParams();
            return EncryptAndGenerateCryptoStore(password, payload, kdfParams);
        }

        public string EncryptAndGenerateCryptoStoreAsJson(string password, byte[] payload)
        {
            var cryptoStore = EncryptAndGenerateCryptoStore(password, payload);
            return JsonCryptoStoreSerialiser.Serialise(cryptoStore);
        }

        public string EncryptAndGenerateCryptoStoreFromStringAsJson(string password, string payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            return EncryptAndGenerateCryptoStoreAsJson(password, Encoding.UTF8.GetBytes(payload));
        }

        public byte[] DecryptCryptoStoreFromJson(string password, string json)
        {
            var cryptoStore = JsonCryptoStoreSerialiser.Deserialise<T>(json);
            return DecryptCryptoStore(password, cryptoStore);
        }

        public byte[] DecryptCryptoStore(string password, CryptoStore<T> cryptoStore)
        {
            if (cryptoStore == null) throw new ArgumentNullException(nameof(cryptoStore));
            return DecryptFromCryptoInfo(password, cryptoStore.Crypto);
        }

        public abstract KeyStore<T> DeserializeKeyStoreFromJson(string json);
        public abstract string SerializeKeyStoreToJson(KeyStore<T> keyStore);

        public virtual byte[] DecryptKeyStore(string password, KeyStore<T> keyStore)
        {
            if (keyStore == null) throw new ArgumentNullException(nameof(keyStore));
            return DecryptFromCryptoInfo(password, keyStore.Crypto);
        }

        public abstract string GetKdfType();

        public virtual string GetCipherType()
        {
            return "aes-128-ctr";
        }

        public KeyStore<T> EncryptAndGenerateKeyStore(string password, byte[] privateKey, string address, T kdfParams)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (privateKey == null) throw new ArgumentNullException(nameof(privateKey));
            if (address == null) throw new ArgumentNullException(nameof(address));
            if (kdfParams == null) throw new ArgumentNullException(nameof(kdfParams));

            ValidatePrivateKey(privateKey);
            var cryptoInfo = CreateCryptoInfo(password, privateKey, kdfParams);
            return BuildKeyStore(address, cryptoInfo);
        }

        public CryptoStore<T> EncryptAndGenerateCryptoStore(string password, byte[] payload, T kdfParams)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (kdfParams == null) throw new ArgumentNullException(nameof(kdfParams));

            var cryptoInfo = CreateCryptoInfo(password, payload, kdfParams);
            return BuildCryptoStore(cryptoInfo);
        }

        public string EncryptAndGenerateKeyStoreAsJson(string password, byte[] privateKey, string addresss, T kdfParams)
        {
            var keyStore = EncryptAndGenerateKeyStore(password, privateKey, addresss, kdfParams);
            return SerializeKeyStoreToJson(keyStore);
        }

        public byte[] DecryptKeyStoreFromJson(string password, string json)
        {
            var keyStore = DeserializeKeyStoreFromJson(json);
            return DecryptKeyStore(password, keyStore);
        }

        protected CryptoInfo<T> CreateCryptoInfo(string password, byte[] payload, T kdfParams)
        {
            var salt = RandomBytesGenerator.GenerateRandomSalt();
            var derivedKey = GenerateDerivedKey(password, salt, kdfParams);
            var cipherKey = KeyStoreCrypto.GenerateCipherKey(derivedKey);
            var iv = RandomBytesGenerator.GenerateRandomInitialisationVector();
            var cipherText = GenerateCipher(payload, iv, cipherKey);
            var mac = KeyStoreCrypto.GenerateMac(derivedKey, cipherText);
            return new CryptoInfo<T>(GetCipherType(), cipherText, iv, mac, salt, kdfParams, GetKdfType());
        }

        private void ValidatePrivateKey(byte[] privateKey)
        {
            if (privateKey.Length != 32)
            {
                var keyValidation = BigIntegers.AsUnsignedByteArray(new BigInteger(privateKey));
                if (keyValidation.Length != 32)
                    throw new ArgumentException("Private key should be 32 bytes", nameof(privateKey));
            }
        }

        private KeyStore<T> BuildKeyStore(string address, CryptoInfo<T> cryptoInfo)
        {
            return new KeyStore<T>
            {
                Version = CurrentVersion,
                Address = address ?? string.Empty,
                Id = Guid.NewGuid().ToString(),
                Crypto = cryptoInfo
            };
        }

        private CryptoStore<T> BuildCryptoStore(CryptoInfo<T> cryptoInfo)
        {
            return new CryptoStore<T>
            {
                Version = CurrentVersion,
                Id = Guid.NewGuid().ToString(),
                Crypto = cryptoInfo
            };
        }

        protected virtual byte[] GenerateCipher(byte[] privateKey, byte[] iv, byte[] cipherKey)
        {
            return KeyStoreCrypto.GenerateAesCtrCipher(iv, cipherKey, privateKey);
        }

        protected abstract byte[] DecryptFromCryptoInfo(string password, CryptoInfo<T> cryptoInfo);
        protected abstract byte[] GenerateDerivedKey(string pasword, byte[] salt, T kdfParams);

        protected abstract T GetDefaultParams();
    }
}
