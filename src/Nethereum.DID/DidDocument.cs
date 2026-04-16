using System.Collections.Generic;
using Nethereum.DID.Serialization;
using Newtonsoft.Json;
#if NET6_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace Nethereum.DID
{
    public class DidDocument
    {
        [JsonProperty("@context")]
        [Newtonsoft.Json.JsonConverter(typeof(ContextConverter))]
#if NET6_0_OR_GREATER
        [JsonPropertyName("@context")]
        [System.Text.Json.Serialization.JsonConverter(typeof(ContextSystemTextJsonConverter))]
#endif
        public List<object> Context { get; set; }

        [JsonProperty("id")]
#if NET6_0_OR_GREATER
        [JsonPropertyName("id")]
#endif
        public string Id { get; set; }

        [JsonProperty("alsoKnownAs", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
        [JsonPropertyName("alsoKnownAs")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
#endif
        public List<string> AlsoKnownAs { get; set; }

        [JsonProperty("controller", NullValueHandling = NullValueHandling.Ignore)]
        [Newtonsoft.Json.JsonConverter(typeof(SingleOrArrayConverter))]
#if NET6_0_OR_GREATER
        [JsonPropertyName("controller")]
        [System.Text.Json.Serialization.JsonConverter(typeof(SingleOrArraySystemTextJsonConverter))]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
#endif
        public List<string> Controller { get; set; }

        [JsonProperty("verificationMethod", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
        [JsonPropertyName("verificationMethod")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
#endif
        public List<VerificationMethod> VerificationMethod { get; set; }

        [JsonProperty("authentication", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
        [JsonPropertyName("authentication")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
#endif
        public List<VerificationRelationship> Authentication { get; set; }

        [JsonProperty("assertionMethod", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
        [JsonPropertyName("assertionMethod")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
#endif
        public List<VerificationRelationship> AssertionMethod { get; set; }

        [JsonProperty("keyAgreement", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
        [JsonPropertyName("keyAgreement")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
#endif
        public List<VerificationRelationship> KeyAgreement { get; set; }

        [JsonProperty("capabilityInvocation", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
        [JsonPropertyName("capabilityInvocation")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
#endif
        public List<VerificationRelationship> CapabilityInvocation { get; set; }

        [JsonProperty("capabilityDelegation", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
        [JsonPropertyName("capabilityDelegation")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
#endif
        public List<VerificationRelationship> CapabilityDelegation { get; set; }

        [JsonProperty("service", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
        [JsonPropertyName("service")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
#endif
        public List<Service> Service { get; set; }

        [Newtonsoft.Json.JsonExtensionData]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonExtensionData]
#endif
        public IDictionary<string, object> AdditionalData { get; set; }

        public static DidDocument CreateDefault(string id)
        {
            return new DidDocument
            {
                Context = new List<object> { DidConstants.DidContextV1 },
                Id = id
            };
        }
    }
}
