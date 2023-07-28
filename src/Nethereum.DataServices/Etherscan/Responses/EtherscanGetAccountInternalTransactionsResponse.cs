using Newtonsoft.Json;

namespace Nethereum.DataServices.Etherscan.Responses
{
    public class EtherscanGetAccountInternalTransactionsResponse
    {
            [JsonProperty("blockNumber")]
            public string BlockNumber { get; set; }

            [JsonProperty("timeStamp")]
            public string TimeStamp { get; set; }

            [JsonProperty("hash")]
            public string Hash { get; set; }

            [JsonProperty("from")]
            public string From { get; set; }

            [JsonProperty("to")]
            public string To { get; set; }

            [JsonProperty("value")]
            public string Value { get; set; }

            [JsonProperty("contractAddress")]
            public string ContractAddress { get; set; }

            [JsonProperty("input")]
            public string Input { get; set; }

            [JsonProperty("type")]
            public string Type { get; set; }

            [JsonProperty("gas")]
            public string Gas { get; set; }

            [JsonProperty("gasUsed")]
            public string GasUsed { get; set; }

            [JsonProperty("traceId")]
            public string TraceId { get; set; }

            [JsonProperty("isError")]
            public string IsError { get; set; }

            [JsonProperty("errCode")]
            public string ErrCode { get; set; }
    }

}
