using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.WalletConnect.DTOs
{
    public class WCTransactionInput
    {
        [JsonProperty("from")]
        public string From { get; set; }

        [JsonProperty("to")]
        public string To { get; set; }

        [JsonProperty("gas", NullValueHandling = NullValueHandling.Ignore)]
        public string Gas { get; set; }

        [JsonProperty("gasPrice", NullValueHandling = NullValueHandling.Ignore)]
        public string GasPrice { get; set; }

        [JsonProperty("nonce", NullValueHandling = NullValueHandling.Ignore)]
        public string Nonce { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public string Data { get; set; } = "0x";

        [JsonProperty(PropertyName = "maxFeePerGas", NullValueHandling = NullValueHandling.Ignore)]
        public string MaxFeePerGas { get; set; }

        [JsonProperty(PropertyName = "maxPriorityFeePerGas", NullValueHandling = NullValueHandling.Ignore)]
        public string MaxPriorityFeePerGas { get; set; }

        [JsonProperty(PropertyName = "type", NullValueHandling = NullValueHandling.Ignore)]
        public string Type { get; set; }

        [JsonProperty(PropertyName = "chainId", NullValueHandling = NullValueHandling.Ignore)]
        public string ChainId { get; set; }
    }


}





