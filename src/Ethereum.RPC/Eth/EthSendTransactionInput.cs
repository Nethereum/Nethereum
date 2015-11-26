using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

namespace Ethereum.RPC.SendTransaction { 

    /// <summary>
    /// Object - The transaction object
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class EthSendTransactionInput { 

        /// <summary>
        ///  DATA, 20 Bytes - The address the transaction is send from.
        /// </summary>
        [JsonProperty(PropertyName = "from")]
        public string From { get; set; }

        /// <summary>
        /// DATA, 20 Bytes - (optional when creating new contract) The address the transaction is directed to.
        /// </summary>
        [JsonProperty(PropertyName = "to")]
        public string To { get; set; }

        /// <summary>
        /// QUANTITY - (optional, default: 90000) Integer of the gas provided for the transaction execution.It will return unused gas.
        /// </summary>
        [JsonProperty(PropertyName = "gas")]
        public int? Gas { get; set; }

        /// <summary>
        /// gasPrice: QUANTITY - (optional, default: To-Be-Determined) Integer of the gasPrice used for each paid gas
        /// </summary>
        [JsonProperty(PropertyName = "gasPrice")]
        public int? GasPrice { get; set; }

        /// <summary>
        /// value: QUANTITY - (optional) Integer of the value send with this transaction
        /// </summary>
        [JsonProperty(PropertyName = "value")]
        public int? Value { get; set; }
        /// <summary>
        /// data: DATA - (optional) The compiled code of a contract
        /// </summary>
        [JsonProperty(PropertyName = "data")]
        public string Data { get; set; }

        /// <summary>
        /// nonce: QUANTITY - (optional) Integer of a nonce. This allows to overwrite your own pending transactions that use the same nonce.
        /// </summary>
        [JsonProperty(PropertyName = "nonce")]
        public int? Nonce { get; set; }

    }
}
