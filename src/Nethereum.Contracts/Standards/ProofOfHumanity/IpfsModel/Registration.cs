using Newtonsoft.Json;

namespace Nethereum.Fx.Nethereum.Contracts.Standards.ProofOfHumanity.IpfsModel
{
    public partial class Registration
    {
        [JsonProperty("fileURI")]
        public string FileUri { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }
}

