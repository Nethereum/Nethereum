using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Nethereum.Besu.RPC.EEA.DTOs
{
    public class EeaTransactionReceipt
    {
        // <summary>
        /// DATA, 20 Bytes - Address of the sender.
        /// </summary>
        [JsonProperty(PropertyName = "from")]
        public string From { get; set; }


        // <summary>
        /// DATA, 20 Bytes - Address of the receiver, if sending ether; otherwise, null.
        /// </summary>
        [JsonProperty(PropertyName = "to")]
        public string To { get; set; }

        /// <summary>
        ///     DATA, 20 Bytes - The contract address created, if the transaction was a contract creation, otherwise null.
        /// </summary>
        [JsonProperty(PropertyName = "contractAddress")]
        public string ContractAddress { get; set; }

        /// <summary>
        ///     RLP-encoded return value of a contract call, if value is returned; otherwise, null
        /// </summary>
        [JsonProperty(PropertyName = "ouput")]
        public string Output { get; set; }

        /// <summary>
        ///     logs: Array - Array of log objects, which this transaction generated.
        /// </summary>
        [JsonProperty(PropertyName = "logs")]
        public JArray Logs { get; set; }
    }
}