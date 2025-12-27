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
            return KeyStoreCrypto.GeneratePbkdf2Sha256DerivedKey(pasword, salt, kdfParams.Count, kdfParams.Dklen);
        }

        protected override Pbkdf2Params GetDefaultParams()
        {
            return new Pbkdf2Params {Dklen = 32, Count = 262144, Prf = "hmac-sha256"};
        }

        public override KeyStore<Pbkdf2Params> DeserializeKeyStoreFromJson(string json)
        {
            return JsonKeyStorePbkdf2Serialiser.DeserialisePbkdf2(json);
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
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (cryptoInfo == null) throw new ArgumentNullException(nameof(cryptoInfo));

            return KeyStoreCrypto.DecryptPbkdf2Sha256(password, cryptoInfo.Mac.HexToByteArray(),
                cryptoInfo.CipherParams.Iv.HexToByteArray(),
                cryptoInfo.CipherText.HexToByteArray(),
                cryptoInfo.Kdfparams.Count,
                cryptoInfo.Kdfparams.Salt.HexToByteArray(),
                cryptoInfo.Kdfparams.Dklen);
        }
    }
}
