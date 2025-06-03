using System;


#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Etherscan.Responses.Account
{
    public class EtherscanGetAccountTransactionsResponse
    {
#if NET8_0_OR_GREATER
        [JsonPropertyName("blockNumber")]
#else
        [JsonProperty("blockNumber")]
#endif
        public string BlockNumber { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("timeStamp")]
#else
        [JsonProperty("timeStamp")]
#endif
        public string TimeStamp { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("hash")]
#else
        [JsonProperty("hash")]
#endif
        public string Hash { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("nonce")]
#else
        [JsonProperty("nonce")]
#endif
        public string Nonce { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("blockHash")]
#else
        [JsonProperty("blockHash")]
#endif
        public string BlockHash { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("transactionIndex")]
#else
        [JsonProperty("transactionIndex")]
#endif
        public string TransactionIndex { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("from")]
#else
        [JsonProperty("from")]
#endif
        public string From { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("to")]
#else
        [JsonProperty("to")]
#endif
        public string To { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("value")]
#else
        [JsonProperty("value")]
#endif
        public string Value { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("gas")]
#else
        [JsonProperty("gas")]
#endif
        public string Gas { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("gasPrice")]
#else
        [JsonProperty("gasPrice")]
#endif
        public string GasPrice { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("isError")]
#else
        [JsonProperty("isError")]
#endif
        public string IsError { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("txreceipt_status")]
#else
        [JsonProperty("txreceipt_status")]
#endif
        public string TxnReceiptStatus { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("input")]
#else
        [JsonProperty("input")]
#endif
        public string Input { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("contractAddress")]
#else
        [JsonProperty("contractAddress")]
#endif
        public string ContractAddress { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("cumulativeGasUsed")]
#else
        [JsonProperty("cumulativeGasUsed")]
#endif
        public string CumulativeGasUsed { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("gasUsed")]
#else
        [JsonProperty("gasUsed")]
#endif
        public string GasUsed { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("confirmations")]
#else
        [JsonProperty("confirmations")]
#endif
        public string Confirmations { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("methodId")]
#else
        [JsonProperty("methodId")]
#endif
        public string MethodId { get; set; }

#if NET8_0_OR_GREATER
        [JsonPropertyName("functionName")]
#else
        [JsonProperty("functionName")]
#endif
        public string FunctionName { get; set; }
    }
}
