using Newtonsoft.Json.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Nethereum.Quorum.RPC.DTOs
{
    public class IstanbulStatus
    {
        /// <summary>
        /// Number of blocks for which sealer activity is retrieved
        /// </summary>
        [JsonProperty(PropertyName = "numBlocks")]
        public long NumberOfBlocks { get; set; }

        /// <summary>
        ///map of strings to numbers - key is the validator and value is the number of blocks sealed by the validator
        /// </summary>
        [JsonProperty(PropertyName = "sealerActivity")]
        public JObject SealerActivity { get; set; }
    }
}
