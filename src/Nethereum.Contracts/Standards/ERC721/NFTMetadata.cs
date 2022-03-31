using Newtonsoft.Json;

namespace Nethereum.Contracts.Standards.ERC721
{
    public class NftMetadata
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("description")]
        public string Description { get; set; }
        [JsonProperty("external_url")]
        public string ExternalUrl { get; set; }
        [JsonProperty("image")]
        public string Image { get; set; }
    }
}
