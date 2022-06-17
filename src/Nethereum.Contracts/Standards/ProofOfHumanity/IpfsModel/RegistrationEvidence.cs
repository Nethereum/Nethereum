using Newtonsoft.Json;

namespace Nethereum.Fx.Nethereum.Contracts.Standards.ProofOfHumanity.IpfsModel
{
    public partial class RegistrationEvidence
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("firstName")]
        public string FirstName { get; set; }

        [JsonProperty("lastName")]
        public string LastName { get; set; }

        [JsonProperty("bio")]
        public string Bio { get; set; }

        [JsonProperty("photo")]
        public string Photo { get; set; }

        [JsonProperty("video")]
        public string Video { get; set; }
    }
}

