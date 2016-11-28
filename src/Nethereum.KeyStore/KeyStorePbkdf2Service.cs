using System;
using Nethereum.KeyStore.Crypto;
using Nethereum.KeyStore.Model;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.KeyStore
{
    public class KeyStorePbkdf2Service : KeyStoreServiceBase<Pbkdf2Params>
    {
        public const string KdfType = "pbkdf2";

        public KeyStorePbkdf2Service()
        {
        }
        public KeyStorePbkdf2Service(IRandomBytesGenerator randomBytesGenerator, KeyStoreCrypto keyStoreCrypto) : base(randomBytesGenerator, keyStoreCrypto)
        {
        }

        protected override byte[] GenerateDerivedKey(byte[] pasword, byte[] salt, Pbkdf2Params kdfParams)
        {
            return KeyStoreCrypto.GeneratePbkdf2Sha256DerivedKey(pasword, salt, kdfParams.Count, kdfParams.Dklen);
        }

        protected override Pbkdf2Params GetDefaultParams()
        {
            return new Pbkdf2Params() { Dklen = 32, Count = 65536, Prf = "hmac-sha256" };
        }

        public override byte[] DecryptKeyStore(string password, KeyStore<Pbkdf2Params> keyStore)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            if (keyStore == null) throw new ArgumentNullException(nameof(keyStore));

            return KeyStoreCrypto.DecryptPbkdf2Sha256(password, keyStore.Crypto.Mac.HexToByteArray(), 
                keyStore.Crypto.CipherParams.Iv.HexToByteArray(),
                keyStore.Crypto.CipherText.HexToByteArray(),
                keyStore.Crypto.Kdfparams.Count,
                keyStore.Crypto.Kdfparams.Salt.HexToByteArray(),
                keyStore.Crypto.Kdfparams.Dklen);
        }

        public override string GetKdfType()
        {
            return KdfType;
        }
    }
}