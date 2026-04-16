using System.Collections.Generic;
using Newtonsoft.Json;
#if NET6_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace Nethereum.DID
{
    public class VerificationMethod
    {
        [JsonProperty("id")]
#if NET6_0_OR_GREATER
        [JsonPropertyName("id")]
#endif
        public string Id { get; set; }

        [JsonProperty("type")]
#if NET6_0_OR_GREATER
        [JsonPropertyName("type")]
#endif
        public string Type { get; set; }

        [JsonProperty("controller")]
#if NET6_0_OR_GREATER
        [JsonPropertyName("controller")]
#endif
        public string Controller { get; set; }

        [JsonProperty("publicKeyMultibase", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
        [JsonPropertyName("publicKeyMultibase")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
#endif
        public string PublicKeyMultibase { get; set; }

        [JsonProperty("publicKeyJwk", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
        [JsonPropertyName("publicKeyJwk")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
#endif
        public object PublicKeyJwk { get; set; }

        [JsonProperty("publicKeyHex", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
        [JsonPropertyName("publicKeyHex")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
#endif
        public string PublicKeyHex { get; set; }

        [JsonProperty("publicKeyBase58", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
        [JsonPropertyName("publicKeyBase58")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
#endif
        public string PublicKeyBase58 { get; set; }

        [JsonProperty("publicKeyBase64", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
        [JsonPropertyName("publicKeyBase64")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
#endif
        public string PublicKeyBase64 { get; set; }

        [JsonProperty("blockchainAccountId", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
        [JsonPropertyName("blockchainAccountId")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
#endif
        public string BlockchainAccountId { get; set; }

        [JsonProperty("ethereumAddress", NullValueHandling = NullValueHandling.Ignore)]
#if NET6_0_OR_GREATER
        [JsonPropertyName("ethereumAddress")]
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
#endif
        public string EthereumAddress { get; set; }

        [Newtonsoft.Json.JsonExtensionData]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonExtensionData]
#endif
        public IDictionary<string, object> AdditionalData { get; set; }
    }
}
