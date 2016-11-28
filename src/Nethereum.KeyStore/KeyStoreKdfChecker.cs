using System;
using Newtonsoft.Json.Linq;

namespace Nethereum.KeyStore
{
    public class KeyStoreKdfChecker
    {
        public enum KdfType
        {
            scrypt,
            pbkdf2
        }

        public KdfType GetKeyStoreKdfType(string json)
        {
            try
            {
                var keyStoreDocument = JObject.Parse(json);
                var kdf = keyStoreDocument.GetValue("crypto", StringComparison.OrdinalIgnoreCase)["kdf"].Value<string>();

                if (kdf == KeyStorePbkdf2Service.KdfType)
                {
                    return KdfType.pbkdf2;
                }

                if (kdf == KeyStoreScryptService.KdfType)
                {
                    return KdfType.scrypt;
                }

                throw new InvalidKdfException(kdf);
            }
            catch (Exception ex)
            {
                throw new Exception("Invalid KeyStore json", ex);
            }
        }
    }
}