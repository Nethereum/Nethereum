using System.Runtime.Serialization;
using Newtonsoft.Json.Linq;

namespace Nethereum.Besu.RPC.EEA.DTOs
{
    public class EeaTransactionReceipt
    {
        // <summary>
        /// DATA, 20 Bytes - Address of the sender.
        /// </summary>
        [DataMember(Name = "from")]
        public string From { get; set; }


        // <summary>
        /// DATA, 20 Bytes - Address of the receiver, if sending ether; otherwise, null.
        /// </summary>
        [DataMember(Name = "to")]
        public string To { get; set; }

        /// <summary>
        ///     DATA, 20 Bytes - The contract address created, if the transaction was a contract creation, otherwise null.
        /// </summary>
        [DataMember(Name = "contractAddress")]
        public string ContractAddress { get; set; }

        /// <summary>
        ///     RLP-encoded return value of a contract call, if value is returned; otherwise, null
        /// </summary>
        [DataMember(Name = "ouput")]
        public string Output { get; set; }

        /// <summary>
        ///     logs: Array - Array of log objects, which this transaction generated.
        /// </summary>
        [DataMember(Name = "logs")]
        public JArray Logs { get; set; }
    }
}