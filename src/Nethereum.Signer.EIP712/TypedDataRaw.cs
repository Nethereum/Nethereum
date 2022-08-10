using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nethereum.Signer.EIP712
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TypedDataRaw
    {
        [JsonProperty(PropertyName = "types")]
        public IDictionary<string, MemberDescription[]> Types { get; set; }

        [JsonProperty(PropertyName = "primaryType")]
        public string PrimaryType { get; set; }
        public MemberValue[] Message { get; set; }
        public MemberValue[] DomainRawValues { get; set; }
    }
}