using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nethereum.Signer.EIP712
{
    public class TypedData<TDomain> where TDomain: IDomain
    {
        [JsonProperty(PropertyName = "types")]
        public IDictionary<string, MemberDescription[]> Types { get; set; }

        [JsonProperty(PropertyName = "primaryType")]
        public string PrimaryType { get; set; }

        [JsonProperty(PropertyName = "domain")]
        public TDomain Domain { get; set; }

        public MemberValue[] Message { get; set; }
    }
}