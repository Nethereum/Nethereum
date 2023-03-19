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
        public HexBigInteger Index { get; set; }

        /// <summary>
        ///     QUANTITY - index of validator that generated withdrawal.
        /// </summary>

        [JsonProperty(PropertyName = "validatorIndex")]
        public HexBigInteger ValidatorIndex { get; set; }

        /// <summary>
        ///     Address - Recipient address for withdrawal value.
        /// </summary>

        [JsonProperty(PropertyName = "address")]
        public string Address { get; set; }

        /// <summary>
        ///     QUANTITY - value contained in withdrawal.
        /// </summary>
        [JsonProperty(PropertyName = "amount")]
        public HexBigInteger Amount { get; set; }
    }
}

