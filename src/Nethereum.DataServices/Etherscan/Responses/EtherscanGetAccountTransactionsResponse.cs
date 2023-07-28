using Newtonsoft.Json;

namespace Nethereum.DataServices.Etherscan.Responses
{
    public class EtherscanGetAccountTransactionsResponse
    {
        [JsonProperty("blockNumber")]
        public string BlockNumber { get; set; }

        [JsonProperty("timeStamp")]
        public string TimeStamp { get; set; }

        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("nonce")]
        public string Nonce { get; set; }

        [JsonProperty("blockHash")]
        public string BlockHash { get; set; }

        [JsonProperty("transactionIndex")]
        public string TransactionIndex { get; set; }

        [JsonProperty("from")]
        public string From { get; set; }

        [JsonProperty("to")]
        public string To { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("gas")]
        public string Gas { get; set; }

        [JsonProperty("gasPrice")]
        public string GasPrice { get; set; }

        [JsonProperty("isError")]
        public string IsError { get; set; }

        [JsonProperty("txreceipt_status")]
        public string TxnReceiptStatus { get; set; }

        [JsonProperty("input")]
        public string Input { get; set; }

        [JsonProperty("contractAddress")]
        public string ContractAddress { get; set; }

        [JsonProperty("cumulativeGasUsed")]
        public string CumulativeGasUsed { get; set; }

        [JsonProperty("gasUsed")]
        public string GasUsed { get; set; }

        [JsonProperty("confirmations")]
        public string Confirmations { get; set; }

        [JsonProperty("methodId")]
        public string MethodId { get; set; }

        [JsonProperty("functionName")]
        public string FunctionName { get; set; }
    }

}
