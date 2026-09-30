using System.Numerics;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

#if NET6_0_OR_GREATER
using System.Text.Json.Serialization;
#endif

namespace Nethereum.RPC.Eth.DTOs
{
    /// <summary>
    /// Fee history for the returned block range. This can be a subsection of the requested range if not all blocks are available.
    /// </summary>
    public class FeeHistoryResult
    {
        /// <summary>
        /// Lowest number block of the returned range.
        /// </summary>
        [JsonProperty(PropertyName = "oldestBlock")]
#if NET6_0_OR_GREATER
        [JsonPropertyName("oldestBlock")]
#endif
        public HexBigInteger OldestBlock { get; set; }

        /// <summary>
        /// An array of block base fees per gas. Includes the next block after the newest of the returned range.
        /// </summary>
        [JsonProperty(PropertyName = "baseFeePerGas")]
#if NET6_0_OR_GREATER
        [JsonPropertyName("baseFeePerGas")]
#endif
        public HexBigInteger[] BaseFeePerGas { get; set; }

        /// <summary>
        /// An array of block gas used ratios. Values between 0 and 1.
        /// </summary>
        [JsonProperty(PropertyName = "gasUsedRatio")]
#if NET6_0_OR_GREATER
        [JsonPropertyName("gasUsedRatio")]
#endif
        public decimal[] GasUsedRatio { get; set; }

        /// <summary>
        /// A two-dimensional array of effective priority fees per gas at the requested block percentiles.
        /// </summary>
        [JsonProperty(PropertyName = "reward")]
#if NET6_0_OR_GREATER
        [JsonPropertyName("reward")]
#endif
        public HexBigInteger[][] Reward { get; set; }

        /// <summary>
        /// An array of base fees per blob gas for the returned block range. Includes the next block
        /// after the newest of the returned range. Zero for blocks before EIP-4844.
        /// </summary>
        [JsonProperty(PropertyName = "baseFeePerBlobGas")]
#if NET6_0_OR_GREATER
        [JsonPropertyName("baseFeePerBlobGas")]
#endif
        public HexBigInteger[] BaseFeePerBlobGas { get; set; }

        [JsonProperty(PropertyName = "blobGasUsedRatio")]
#if NET6_0_OR_GREATER
        [JsonPropertyName("blobGasUsedRatio")]
#endif
        public decimal[] BlobGasUsedRatio { get; set; }
    }
}