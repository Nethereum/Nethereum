using Newtonsoft.Json;

namespace Nethereum.KeyStore.Model
{
    public class KeyStore<TKdfParams> where TKdfParams : KdfParams
    {

        [JsonProperty("crypto")]
        public CryptoInfo<TKdfParams> Crypto { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }
    }
}