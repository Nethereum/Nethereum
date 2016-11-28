using Newtonsoft.Json;

namespace Nethereum.KeyStore.Model
{
    public class KdfParams
    {
        [JsonProperty("dklen")]
        public int Dklen { get; set; }

        [JsonProperty("salt")]
        public string Salt { get; set; }
    }
}