using Newtonsoft.Json;
using System;

namespace Nethereum.DataServices.FourByteDirectory.Responses
{
    public class FourByteDirectorySignature
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("text_signature")]
        public string TextSignature { get; set; }

        [JsonProperty("hex_signature")]
        public string HexSignature { get; set; }

        [JsonProperty("bytes_signature")]
        public string BytesSignature { get; set; }
    }
}
