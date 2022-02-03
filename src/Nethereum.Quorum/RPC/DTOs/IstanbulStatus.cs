using Newtonsoft.Json.Linq;
using System.Runtime.Serialization;

namespace Nethereum.Quorum.RPC.DTOs
{
    [DataContract]
    public class IstanbulStatus
    {
        /// <summary>
        /// Number of blocks for which sealer activity is retrieved
        /// </summary>
        [DataMember(Name = "numBlocks")]
        public long NumberOfBlocks { get; set; }

        /// <summary>
        ///map of strings to numbers - key is the validator and value is the number of blocks sealed by the validator
        /// </summary>
        [DataMember(Name = "sealerActivity")]
        public JObject SealerActivity { get; set; }
    }
}
