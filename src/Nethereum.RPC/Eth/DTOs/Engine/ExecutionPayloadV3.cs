using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs.Engine
{
    public class ExecutionPayloadV3 : ExecutionPayloadV2
    {
        [JsonProperty(PropertyName = "blobGasUsed")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("blobGasUsed")]
#endif
        public HexBigInteger BlobGasUsed { get; set; }

        [JsonProperty(PropertyName = "excessBlobGas")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("excessBlobGas")]
#endif
        public HexBigInteger ExcessBlobGas { get; set; }
    }
}
