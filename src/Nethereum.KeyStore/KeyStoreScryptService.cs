using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.KeyStore.Crypto;
using Nethereum.KeyStore.JsonDeserialisation;
using Nethereum.KeyStore.Model;

namespace Nethereum.KeyStore
{
    public class KeyStoreScryptService : KeyStoreServiceBase<ScryptParams>
    {
        public const string KdfType = "scrypt";

        public KeyStoreScryptService()
        {
        }

        public KeyStoreScryptService(IRandomBytesGenerator randomBytesGenerator, KeyStoreCrypto keyStoreCrypto) : base(
            randomBytesGenerator, keyStoreCrypto)
        {
        }

        public KeyStoreScryptService(IRandomBytesGenerator randomBytesGenerator) : base(randomBytesGenerator)
        {
        }

        protected override byte[] GenerateDerivedKey(string password, byte[] salt, ScryptParams kdfParams)
        {
            return KeyStoreCrypto.GenerateDerivedScryptKey(KeyStoreCrypto.GetPasswordAsBytes(password), salt,
                kdfParams.N, kdfParams.R,
                kdfParams.P, kdfParams.Dklen);
        }

        protected override ScryptParams GetDefaultParams()
        {
            return new ScryptParams {Dklen = 32, N = 262144, R = 1, P = 8};
        }

        public override KeyStore<ScryptParams> DeserializeKeyStoreFromJson(string json)
        {
            return KeyStoreParameterValidator.Parse(() => JsonKeyStoreScryptSerialiser.DeserialiseScrypt(json));
        }

        public override string SerializeKeyStoreToJson(KeyStore<ScryptParams> keyStore)
        {
            return JsonKeyStoreScryptSerialiser.SerialiseScrypt(keyStore);
        }

        public override string GetKdfType()
        {
            return KdfType;
        }

        protected override byte[] DecryptFromCryptoInfo(string password, CryptoInfo<ScryptParams> cryptoInfo)
        {
            return DecryptFromCryptoInfo(password, cryptoInfo, KdfLimits);
        }

        protected override byte[] DecryptFromCryptoInfo(string password, CryptoInfo<ScryptParams> cryptoInfo, KeyStoreKdfLimits limits)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (cryptoInfo == null) throw new ArgumentNullException(nameof(cryptoInfo));
            KeyStoreParameterValidator.ValidateEnvelope(cryptoInfo, KdfType);
            KeyStoreParameterValidator.ValidateScrypt(cryptoInfo.Kdfparams, limits);
            return KeyStoreCrypto.DecryptScrypt(password, KeyStoreParameterValidator.RequireHex(cryptoInfo.Mac, "mac", 0, int.MaxValue),
                KeyStoreParameterValidator.RequireHex(cryptoInfo.CipherParams.Iv, "iv", 16, 16),
                KeyStoreParameterValidator.RequireHex(cryptoInfo.CipherText, "ciphertext", 0, int.MaxValue),
                cryptoInfo.Kdfparams.N,
                cryptoInfo.Kdfparams.P,
                cryptoInfo.Kdfparams.R,
                KeyStoreParameterValidator.RequireHex(cryptoInfo.Kdfparams.Salt, "salt", 1, int.MaxValue),
                cryptoInfo.Kdfparams.Dklen,
                limits);
        }
    }
}
