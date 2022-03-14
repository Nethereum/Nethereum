using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using System.Runtime.Serialization;

namespace Nethereum.RPC.Eth.DTOs
{
    /// <summary>
    ///     Object - The transaction call object
    /// </summary>
    [DataContract]
    public class CallInput
    {
        private string _from;
        private string _to;
        private string _data;

        public CallInput()
        {
        }

        public CallInput(string data, string addressTo)
        {
            Data = data;
            To = addressTo;
        }

        public CallInput(string data, string addressTo, HexBigInteger value) : this(data, addressTo)
        {
            Value = value;
        }

        public CallInput(string data, string addressTo, string addressFrom, HexBigInteger gas, HexBigInteger value)
            : this(data, addressTo, value)
        {
            From = addressFrom;
            Gas = gas;
        }

        public CallInput(string data, string addressTo, string addressFrom, HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value)
            : this(data, addressTo, addressFrom, gas, value)
        {
            GasPrice = gasPrice;
        }

        public CallInput(string data, string addressTo, string addressFrom, HexBigInteger gas, HexBigInteger value, HexBigInteger type, HexBigInteger maxFeePerGas, HexBigInteger maxPriorityFeePerGas)
        {
            Data = data;
            To = addressTo;
            From = addressFrom;
            Gas = gas;
            Value = value;
            Type = type;
            MaxFeePerGas = maxFeePerGas;
            MaxPriorityFeePerGas = maxPriorityFeePerGas;
        }

        public CallInput(string data, string addressFrom, HexBigInteger gas, HexBigInteger value)
            : this(data, null, value)
        {
            From = addressFrom;
            Gas = gas;
        }

        public CallInput(string data, HexBigInteger gas, string addressFrom)
        {
            Data = data;
            Gas = gas;
            From = addressFrom;
        }

        /// <summary>
        ///     DATA, 20 Bytes - The address the transaction is send from.
        /// </summary>
        [DataMember(Name = "from")]
        public string From
        {
            get { return _from.EnsureHexPrefix(); }
            set { _from = value; }
        }

        /// <summary>
        ///     DATA, 20 Bytes - (optional when creating new contract) The address the transaction is directed to.
        /// </summary>
        [DataMember(Name = "to")]
        public string To
        {
            get { return _to.EnsureHexPrefix(); }
            set { _to = value; }
        }

        /// <summary>
        ///     QUANTITY - (optional, default: 90000) Integer of the gas provided for the transaction execution.It will return
        ///     unused gas.
        /// </summary>
        [DataMember(Name = "gas")]
        public HexBigInteger Gas { get; set; }

        /// <summary>
        ///     gasPrice: QUANTITY - (optional, default: To-Be-Determined) Integer of the gasPrice used for each paid gas
        /// </summary>
        [DataMember(Name = "gasPrice")]
        public HexBigInteger GasPrice { get; set; }

        /// <summary>
        ///     value: QUANTITY - (optional) Integer of the value send with this transaction
        /// </summary>
        [DataMember(Name = "value")]
        public HexBigInteger Value { get; set; }

        /// <summary>
        ///     data: DATA - (optional) The compiled code of a contract
        /// </summary>
        [DataMember(Name = "input")]
        public string Data
        {
            get { return _data.EnsureHexPrefix(); }
            set { _data = value; }
        }

        /// <summary>
        ///   QUANTITY - Max Fee Per Gas provided by the sender in Wei.
        /// </summary>
       [DataMember(Name = "maxFeePerGas")]
        public HexBigInteger MaxFeePerGas { get; set; }

        /// <summary>
        ///   QUANTITY - Max Priority Fee Per Gas provided by the sender in Wei.
        /// </summary>
        [DataMember(Name = "maxPriorityFeePerGas")]
        public HexBigInteger MaxPriorityFeePerGas { get; set; }

        /// <summary>
        ///    QUANTITY - The transaction type.
        /// </summary>
        [DataMember(Name = "type")]
        public HexBigInteger Type { get; set; }

        /// <summary>
        /// chainId :Chain ID that this transaction is valid on.
        /// </summary>
        [DataMember(Name = "chainId")]
        public HexBigInteger ChainId { get; set; }

    }
}