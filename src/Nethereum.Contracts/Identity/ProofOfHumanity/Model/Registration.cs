using Newtonsoft.Json;

namespace Nethereum.Contracts.Identity.ProofOfHumanity.Model
{
    public partial class Registration
    {
        [JsonProperty("fileURI")]
        public string FileUri { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }
}

