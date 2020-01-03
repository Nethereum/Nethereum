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
            return new ScryptParams {Dklen = 32, N = 8192, R = 8, P = 1};
        }

        public override KeyStore<ScryptParams> DeserializeKeyStoreFromJson(string json)
        {
            return JsonKeyStoreScryptSerialiser.DeserialiseScrypt(json);
        }

        public override string SerializeKeyStoreToJson(KeyStore<ScryptParams> keyStore)
        {
            return JsonKeyStoreScryptSerialiser.SerialiseScrypt(keyStore);
        }

        public override byte[] DecryptKeyStore(string password, KeyStore<ScryptParams> keyStore)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (keyStore == null) throw new ArgumentNullException(nameof(keyStore));

            return KeyStoreCrypto.DecryptScrypt(password, keyStore.Crypto.Mac.HexToByteArray(),
                keyStore.Crypto.CipherParams.Iv.HexToByteArray(),
                keyStore.Crypto.CipherText.HexToByteArray(),
                keyStore.Crypto.Kdfparams.N,
                keyStore.Crypto.Kdfparams.P,
                keyStore.Crypto.Kdfparams.R,
                keyStore.Crypto.Kdfparams.Salt.HexToByteArray(),
                keyStore.Crypto.Kdfparams.Dklen);
        }

        public override string GetKdfType()
        {
            return KdfType;
        }
    }
}
