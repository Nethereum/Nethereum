using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    /// <summary>
    /// Object - The transaction call object
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class CallInput
    {
        public CallInput()
        {
        }

        public CallInput(string data, string addressTo)
        {
            this.Data = data;
            this.To = addressTo;
            this.Gas = new HexBigInteger(900000);
        }

        public CallInput(string data, string addressTo, HexBigInteger value):this(data, addressTo)
        {
            this.Value = value;
        }

        public CallInput(string data, string addressTo, string adddressFrom, HexBigInteger value) : this(data, addressTo, value)
        {
            this.From = adddressFrom;
        }

        public CallInput(string data, HexBigInteger gas, string addressFrom)
        {
            this.Gas = new HexBigInteger(900000);
            this.Data = data;
            //this.Gas = gas;
            this.From = addressFrom;
        }


    
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
        public HexBigInteger Gas { get; set; }

        /// <summary>
        /// gasPrice: QUANTITY - (optional, default: To-Be-Determined) Integer of the gasPrice used for each paid gas
        /// </summary>
        [JsonProperty(PropertyName = "gasPrice")]
        public HexBigInteger GasPrice { get; set; }

        /// <summary>
        /// value: QUANTITY - (optional) Integer of the value send with this transaction
        /// </summary>
        [JsonProperty(PropertyName = "value")]
        public HexBigInteger Value { get; set; }
        /// <summary>
        /// data: DATA - (optional) The compiled code of a contract
        /// </summary>
        [JsonProperty(PropertyName = "data")]
        public string Data { get; set; }

    }
}