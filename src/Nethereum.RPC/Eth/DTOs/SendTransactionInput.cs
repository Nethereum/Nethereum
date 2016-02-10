using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs {
    /// <summary>
    /// Object - The transaction object
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class TransactionInput:CallInput
    {
        public TransactionInput()
        {
        }

        public TransactionInput(string data, string addressTo):base(data, addressTo)
        {
           
        }

        public TransactionInput(string data, string addressTo, HexBigInteger value):base(data, addressTo, value)
        {
            
        }
        
        public TransactionInput(string data, string addressTo, string adddressFrom, HexBigInteger gas, HexBigInteger value) : base(data, addressTo, adddressFrom, gas, value)
        {
           
        }

        public TransactionInput(string data, HexBigInteger gas, string adddressFrom) : base(data, gas, adddressFrom)
        {

        }
        /// <summary>
        /// nonce: QUANTITY - (optional) Integer of a nonce. This allows to overwrite your own pending transactions that use the same nonce.
        /// </summary>
        [JsonProperty(PropertyName = "nonce")]
        public HexBigInteger Nonce { get; set; }

    }
}
