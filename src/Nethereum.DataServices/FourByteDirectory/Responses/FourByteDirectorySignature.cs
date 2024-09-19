using System;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization; 
#else
using Newtonsoft.Json; 
#endif

namespace Nethereum.DataServices.FourByteDirectory.Responses
{
    public class FourByteDirectorySignature
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("id")]
#else
        [JsonProperty("id")]
#endif
        public int Id { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("created_at")]
#else
        [JsonProperty("created_at")]
#endif
        public DateTime CreatedAt { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("text_signature")]
#else
        [JsonProperty("text_signature")]
#endif
        public string TextSignature { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("hex_signature")]
#else
        [JsonProperty("hex_signature")]
#endif
        public string HexSignature { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("bytes_signature")]
#else
        [JsonProperty("bytes_signature")]
#endif
        public string BytesSignature { get; set; }
    }
}

