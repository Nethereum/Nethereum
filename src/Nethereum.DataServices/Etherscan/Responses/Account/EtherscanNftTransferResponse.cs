using System;

#if NET8_0_OR_GREATER
using System.Text.Json.Serialization;
#else
using Newtonsoft.Json;
#endif

namespace Nethereum.DataServices.Etherscan.Responses.Account
{
    public class EtherscanNftTransferResponse : EtherscanTokenTransferResponse
    {
#if NET8_0_OR_GREATER
    [JsonPropertyName("tokenID")]
#else
        [JsonProperty("tokenID")]
#endif
        public string TokenID { get; set; }
    }
}