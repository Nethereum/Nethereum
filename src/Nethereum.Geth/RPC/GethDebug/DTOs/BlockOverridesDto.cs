using System.Collections.Generic;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.Geth.RPC.Debug.DTOs
{
    /// <summary>
    /// The fields of this object customize the block as part of which a call is simulated.
    /// </summary>
    public class BlockOverridesDto
    {
        /// <summary>
        /// Block number.
        /// </summary>
        [JsonProperty("number")]
        public HexBigInteger Number { get; set; }

        /// <summary>
        /// The previous value of randomness beacon.
        /// </summary>
        [JsonProperty("prevRandao")]
        public HexBigInteger PrevRandao { get; set; }

        /// <summary>
        /// Block timestamp.
        /// </summary>
        [JsonProperty("time")]
        public HexBigInteger Time { get; set; }

        /// <summary>
        /// Gas limit
        /// </summary>
        [JsonProperty("gasLimit")]
        public HexBigInteger GasLimit { get; set; }

        /// <summary>
        /// Fee recipient (also known as coinbase).
        /// </summary>
        [JsonProperty("feeRecipient")]
        public string FeeRecipient { get; set; }

        /// <summary>
        /// Withdrawals made by validators.
        /// </summary>
        [JsonProperty("withdrawals")]
        public List<Withdrawal> Withdrawals { get; set; }

        /// <summary>
        /// Base fee per unit of gas (see EIP-1559).
        /// </summary>
        [JsonProperty("baseFeePerGas")]
        public string BaseFeePerGas { get; set; }

        /// <summary>
        /// Base fee per unit of blob gas (see EIP-4844).
        /// </summary>
        [JsonProperty("blobBaseFee")]
        public HexBigInteger BlobBaseFee { get; set; }
        
    }
    
    public class Withdrawal
    {
        [JsonProperty("index")]
        public HexBigInteger Index { get; set; }

        [JsonProperty("validatorIndex")]
        public HexBigInteger ValidatorIndex { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("amount")]
        public HexBigInteger Amount { get; set; }
    }
}