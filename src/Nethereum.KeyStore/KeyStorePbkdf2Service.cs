using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.KeyStore.Crypto;
using Nethereum.KeyStore.JsonDeserialisation;
using Nethereum.KeyStore.Model;

namespace Nethereum.KeyStore
{
    public class KeyStorePbkdf2Service : KeyStoreServiceBase<Pbkdf2Params>
    {
        public const string KdfType = "pbkdf2";

        public KeyStorePbkdf2Service()
        {
        }

        public KeyStorePbkdf2Service(IRandomBytesGenerator randomBytesGenerator, KeyStoreCrypto keyStoreCrypto) : base(
            randomBytesGenerator, keyStoreCrypto)
        {
        }

        public KeyStorePbkdf2Service(IRandomBytesGenerator randomBytesGenerator) : base(randomBytesGenerator)
        {
        }

        protected override byte[] GenerateDerivedKey(string pasword, byte[] salt, Pbkdf2Params kdfParams)
        {
            return KeyStoreCrypto.GeneratePbkdf2Sha256DerivedKey(pasword, salt, kdfParams.Count, kdfParams.Dklen,
                new KeyStoreKdfLimits { MaxPbkdf2Iterations = int.MaxValue });
        }

        protected override Pbkdf2Params GetDefaultParams()
        {
            return new Pbkdf2Params {Dklen = 32, Count = 262144, Prf = "hmac-sha256"};
        }

        public override KeyStore<Pbkdf2Params> DeserializeKeyStoreFromJson(string json)
        {
            return KeyStoreParameterValidator.Parse(() => JsonKeyStorePbkdf2Serialiser.DeserialisePbkdf2(json));
        }

        public override string SerializeKeyStoreToJson(KeyStore<Pbkdf2Params> keyStore)
        {
            return JsonKeyStorePbkdf2Serialiser.SerialisePbkdf2(keyStore);
        }

        public override string GetKdfType()
        {
            return KdfType;
        }

        protected override byte[] DecryptFromCryptoInfo(string password, CryptoInfo<Pbkdf2Params> cryptoInfo)
        {
            return DecryptFromCryptoInfo(password, cryptoInfo, KdfLimits);
        }

        protected override byte[] DecryptFromCryptoInfo(string password, CryptoInfo<Pbkdf2Params> cryptoInfo, KeyStoreKdfLimits limits)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (cryptoInfo == null) throw new ArgumentNullException(nameof(cryptoInfo));
            KeyStoreParameterValidator.ValidateEnvelope(cryptoInfo, KdfType);
            KeyStoreParameterValidator.ValidatePbkdf2(cryptoInfo.Kdfparams, limits);
            return KeyStoreCrypto.DecryptPbkdf2Sha256(password, KeyStoreParameterValidator.RequireHex(cryptoInfo.Mac, "mac", 0, int.MaxValue),
                KeyStoreParameterValidator.RequireHex(cryptoInfo.CipherParams.Iv, "iv", 16, 16),
                KeyStoreParameterValidator.RequireHex(cryptoInfo.CipherText, "ciphertext", 0, int.MaxValue),
                cryptoInfo.Kdfparams.Count,
                KeyStoreParameterValidator.RequireHex(cryptoInfo.Kdfparams.Salt, "salt", 1, int.MaxValue),
                cryptoInfo.Kdfparams.Dklen,
                limits);
        }
    }
}
