using System;
using System.Collections.Generic;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Sourcify.Responses
{
    public class SourcifyProxyResolution
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("isProxy")]
#else
        [JsonProperty("isProxy")]
#endif
        public bool IsProxy { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("proxyType")]
#else
        [JsonProperty("proxyType")]
#endif
        public string ProxyType { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("implementations")]
#else
        [JsonProperty("implementations")]
#endif
        public List<SourcifyProxyImplementation> Implementations { get; set; }
    }

    public class SourcifyProxyImplementation
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("address")]
#else
        [JsonProperty("address")]
#endif
        public string Address { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("name")]
#else
        [JsonProperty("name")]
#endif
        public string Name { get; set; }
    }
}
