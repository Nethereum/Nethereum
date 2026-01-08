using System;
using System.Collections.Generic;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Sourcify.Responses
{
    public class SourcifySignaturesInfo
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("function")]
#else
        [JsonProperty("function")]
#endif
        public List<SourcifySignatureItem> Function { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("event")]
#else
        [JsonProperty("event")]
#endif
        public List<SourcifySignatureItem> Event { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("error")]
#else
        [JsonProperty("error")]
#endif
        public List<SourcifySignatureItem> Error { get; set; }
    }

    public class SourcifySignatureItem
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("signature")]
#else
        [JsonProperty("signature")]
#endif
        public string Signature { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("signatureHash32")]
#else
        [JsonProperty("signatureHash32")]
#endif
        public string SignatureHash32 { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("signatureHash4")]
#else
        [JsonProperty("signatureHash4")]
#endif
        public string SignatureHash4 { get; set; }
    }
}
