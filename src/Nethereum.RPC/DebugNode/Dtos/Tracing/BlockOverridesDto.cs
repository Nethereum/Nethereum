using System.Collections.Generic;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.DebugNode.Dtos.Tracing
{
    public class BlockOverridesDto
    {
        [JsonProperty("number")]
        public HexBigInteger Number { get; set; }

        [JsonProperty("prevRandao")]
        public HexBigInteger PrevRandao { get; set; }

        [JsonProperty("time")]
        public HexBigInteger Time { get; set; }

        [JsonProperty("gasLimit")]
        public HexBigInteger GasLimit { get; set; }

        [JsonProperty("feeRecipient")]
        public string FeeRecipient { get; set; }

        [JsonProperty("withdrawals")]
        public List<Withdrawal> Withdrawals { get; set; }

        [JsonProperty("baseFeePerGas")]
        public string BaseFeePerGas { get; set; }

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
