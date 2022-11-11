using System.Runtime.Serialization;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;

namespace Nethereum.Quorum.RPC.DTOs
{

    public class PrivateTransactionInput : TransactionInput
    {
        public PrivateTransactionInput()
        {
        }


        public PrivateTransactionInput(TransactionInput transaction, string[] privateFor, string privateFrom, int privacyFlag, string[] mandatoryFor): this(transaction, privateFor, privateFrom)
        {
            PrivacyFlag = privacyFlag;
            MandatoryFor = mandatoryFor;
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

        [JsonProperty(PropertyName =  "privateFrom")]
        public string PrivateFrom { get; set; }

        [JsonProperty(PropertyName =  "privateFor")]
        public string[] PrivateFor { get; set; }

        [JsonProperty(PropertyName = "privacyFlag")]
        public int PrivacyFlag { get; set; }

        [JsonProperty(PropertyName = "mandatoryFor")]
        public string[] MandatoryFor { get; set; }
    }

    
}