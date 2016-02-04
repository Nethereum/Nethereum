using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.Transactions {
    /// <summary>
    /// Object - The transaction object
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class EthSendTransactionInput:EthCallTransactionInput
    {
        public EthSendTransactionInput()
        {
        }

        public EthSendTransactionInput(string data, string addressTo):base(data, addressTo)
        {
           
        }

        public EthSendTransactionInput(string data, string addressTo, HexBigInteger value):base(data, addressTo, value)
        {
            
        }
        
        public EthSendTransactionInput(string data, string addressTo, string adddressFrom, HexBigInteger value) : base(data, addressTo, adddressFrom, value)
        {
           
        }

        public EthSendTransactionInput(string data, HexBigInteger gas, string adddressFrom) : base(data, gas, adddressFrom)
        {

        }
        /// <summary>
        /// nonce: QUANTITY - (optional) Integer of a nonce. This allows to overwrite your own pending transactions that use the same nonce.
        /// </summary>
        [JsonProperty(PropertyName = "nonce")]
        public HexBigInteger Nonce { get; set; }

    }
}
