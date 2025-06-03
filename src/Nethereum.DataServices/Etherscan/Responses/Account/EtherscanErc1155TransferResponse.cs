using System;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Etherscan.Responses.Account
{
    public class EtherscanErc1155TransferResponse : EtherscanNftTransferResponse
    {
#if NET8_0_OR_GREATER
    [JsonPropertyName("logIndex")]
#else
        [JsonProperty("logIndex")]
#endif
        public string LogIndex { get; set; }
    }
}