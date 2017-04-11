using System;
using Nethereum.KeyStore.Crypto;
using Nethereum.KeyStore.Model;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.KeyStore
{
    public class KeyStoreScryptService: KeyStoreServiceBase<ScryptParams>
    {
        public const string KdfType = "scrypt";

        public KeyStoreScryptService() 
        {
        }

        public KeyStoreScryptService(IRandomBytesGenerator randomBytesGenerator, KeyStoreCrypto keyStoreCrypto) : base(randomBytesGenerator, keyStoreCrypto)
        {
        }

        public KeyStoreScryptService(IRandomBytesGenerator randomBytesGenerator) : base(randomBytesGenerator)
        {
        }

        protected override byte[] GenerateDerivedKey(string password, byte[] salt, ScryptParams kdfParams)
        {
            return KeyStoreCrypto.GenerateDerivedScryptKey(KeyStoreCrypto.GetPasswordAsBytes(password), salt, kdfParams.N, kdfParams.R,
                kdfParams.P, kdfParams.Dklen);
        }

        protected override ScryptParams GetDefaultParams()
        {
            return new ScryptParams() { Dklen = 32, N = 262144, R = 1, P = 8 };
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