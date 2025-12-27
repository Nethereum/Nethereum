using Newtonsoft.Json;

namespace Nethereum.KeyStore.Model
{
    public class CryptoStore<TKdfParams> where TKdfParams : KdfParams
    {
        [JsonProperty("crypto")]
        public CryptoInfo<TKdfParams> Crypto { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }
    }
}
