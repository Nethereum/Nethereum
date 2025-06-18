using Newtonsoft.Json;
using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.ABI.EIP712
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

    public static class TypedDataRawExtensions
    {
        public static BigInteger? GetChainIdFromDomain(this TypedDataRaw typedData)
        {
            if (!typedData.Types.TryGetValue("EIP712Domain", out var domainMembers))
                return null;

            for (int i = 0; i < domainMembers.Length; i++)
            {
                var member = domainMembers[i];
                if (member.Type == "uint256" && member.Name == "chainId")
                {
                    if (i < typedData.DomainRawValues.Length && typedData.DomainRawValues[i].Value is BigInteger value)
                    {
                        return value;
                    }
                }
            }

            return null;
        }
    }
}