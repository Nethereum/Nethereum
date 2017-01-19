using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;

namespace Nethereum.Quorum.RPC.DTOs
{
    public class PrivateTransactionInput : TransactionInput
    {
        public PrivateTransactionInput()
        {
        }

        public PrivateTransactionInput(TransactionInput transaction, string[] privateFor, string privateFrom)
        {
            PrivateFrom = privateFrom;
            PrivateFor = privateFor;
            From = transaction.From;
            Gas = transaction.Gas;
            GasPrice = transaction.GasPrice;
            Nonce = transaction.Nonce;
            To = transaction.To;
            Data = transaction.Data;
            Value = transaction.Value;
        }

        [JsonProperty(PropertyName = "privateFrom")]
        public string PrivateFrom { get; set; }

        [JsonProperty(PropertyName = "privateFor")]
        public string[] PrivateFor { get; set; }
    }
}