using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;
namespace Nethereum.RPC.Eth.DTOs
{
    public class Withdrawal
    {
        /// <summary>
        ///     QUANTITY - index of withdrawal.
        /// </summary>

        [JsonProperty(PropertyName = "index")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("index")]
#endif
        public HexBigInteger Index { get; set; }

        /// <summary>
        ///     QUANTITY - index of validator that generated withdrawal.
        /// </summary>

        [JsonProperty(PropertyName = "validatorIndex")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("validatorIndex")]
#endif
        public HexBigInteger ValidatorIndex { get; set; }

        /// <summary>
        ///     Address - Recipient address for withdrawal value.
        /// </summary>

        [JsonProperty(PropertyName = "address")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("address")]
#endif
        public string Address { get; set; }

        /// <summary>
        ///     QUANTITY - value contained in withdrawal.
        /// </summary>
        [JsonProperty(PropertyName = "amount")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("amount")]
#endif
        public HexBigInteger Amount { get; set; }
    }
}

