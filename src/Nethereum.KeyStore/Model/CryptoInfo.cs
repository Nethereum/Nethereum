using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;

namespace Nethereum.KeyStore.Model
{
    public class CryptoInfo<TKdfParams> where TKdfParams : KdfParams
    {
        public CryptoInfo()
        {

        }

        public CryptoInfo(string cipher, byte[] cipherText, byte[] iv, byte[] mac, byte[] salt, TKdfParams kdfParams, string kdfType)
        {
            Cipher = cipher;
            CipherText = cipherText.ToHex();
            Mac = mac.ToHex();
            CipherParams = new CipherParams(iv);
            Kdfparams = kdfParams;
            Kdfparams.Salt = salt.ToHex();
            Kdf = kdfType;
        }

        [JsonProperty("cipher")]
        public string Cipher { get; set; }

        [JsonProperty("ciphertext")]
        public string CipherText { get; set; }

        [JsonProperty("cipherparams")]
        public CipherParams CipherParams { get; set; }

        [JsonProperty("kdf")]
        public string Kdf { get; set; }

        [JsonProperty("mac")]
        public string Mac { get; set; }

        [JsonProperty("kdfparams")]
        public TKdfParams Kdfparams { get; set; }
    }
}