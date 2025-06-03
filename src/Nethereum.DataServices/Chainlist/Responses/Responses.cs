using System;
using System.Collections.Generic;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Chainlist.Responses
{

        public class ChainlistChainInfo
        {
#if NET8_0_OR_GREATER
        [JsonPropertyName("name")]
#else
            [JsonProperty("name")]
#endif
            public string Name { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("chain")]
#else
            [JsonProperty("chain")]
#endif
            public string Chain { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("icon")]
#else
            [JsonProperty("icon")]
#endif
            public string Icon { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("rpc")]
#else
            [JsonProperty("rpc")]
#endif
            public List<ChainlistRpc> Rpc { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("features")]
#else
            [JsonProperty("features")]
#endif
            public List<ChainlistFeature> Features { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("faucets")]
#else
            [JsonProperty("faucets")]
#endif
            public List<string> Faucets { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("nativeCurrency")]
#else
            [JsonProperty("nativeCurrency")]
#endif
            public ChainlistNativeCurrency NativeCurrency { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("infoURL")]
#else
            [JsonProperty("infoURL")]
#endif
            public string InfoURL { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("shortName")]
#else
            [JsonProperty("shortName")]
#endif
            public string ShortName { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("chainId")]
#else
            [JsonProperty("chainId")]
#endif
            public long ChainId { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("networkId")]
#else
            [JsonProperty("networkId")]
#endif
            public long NetworkId { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("slip44")]
#else
            [JsonProperty("slip44")]
#endif
            public long? Slip44 { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("ens")]
#else
            [JsonProperty("ens")]
#endif
            public ChainlistEns Ens { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("explorers")]
#else
            [JsonProperty("explorers")]
#endif
            public List<ChainlistExplorer> Explorers { get; set; }
        }

        public class ChainlistRpc
        {
#if NET8_0_OR_GREATER
        [JsonPropertyName("url")]
#else
            [JsonProperty("url")]
#endif
            public string Url { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("tracking")]
#else
            [JsonProperty("tracking")]
#endif
            public string Tracking { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("isOpenSource")]
#else
            [JsonProperty("isOpenSource")]
#endif
            public bool? IsOpenSource { get; set; }
        }

        public class ChainlistFeature
        {
#if NET8_0_OR_GREATER
        [JsonPropertyName("name")]
#else
            [JsonProperty("name")]
#endif
            public string Name { get; set; }
        }

        public class ChainlistNativeCurrency
        {
#if NET8_0_OR_GREATER
        [JsonPropertyName("name")]
#else
            [JsonProperty("name")]
#endif
            public string Name { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("symbol")]
#else
            [JsonProperty("symbol")]
#endif
            public string Symbol { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("decimals")]
#else
            [JsonProperty("decimals")]
#endif
            public long Decimals { get; set; }
        }

        public class ChainlistEns
        {
#if NET8_0_OR_GREATER
        [JsonPropertyName("registry")]
#else
            [JsonProperty("registry")]
#endif
            public string Registry { get; set; }
        }

        public class ChainlistExplorer
        {
#if NET8_0_OR_GREATER
        [JsonPropertyName("name")]
#else
            [JsonProperty("name")]
#endif
            public string Name { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("url")]
#else
            [JsonProperty("url")]
#endif
            public string Url { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("standard")]
#else
            [JsonProperty("standard")]
#endif
            public string Standard { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("icon")]
#else
            [JsonProperty("icon")]
#endif
            public string Icon { get; set; }
        }
    }

