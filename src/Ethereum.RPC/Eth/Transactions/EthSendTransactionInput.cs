using Newtonsoft.Json;

namespace Ethereum.RPC.Eth {
    /// <summary>
    /// Object - The transaction object
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class EthSendTransactionInput:EthCallTransactionInput
    { 
            /// <summary>
        /// nonce: QUANTITY - (optional) Integer of a nonce. This allows to overwrite your own pending transactions that use the same nonce.
        /// </summary>
        [JsonProperty(PropertyName = "nonce")]
        public HexBigInteger Nonce { get; set; }

    }
}
