using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.AccountAbstraction.DTOs
{
    /// <summary>
    /// Response of eth_getUserOperationByHash (ERC-7769): the UserOperation wrapped together
    /// with its inclusion context. blockNumber/blockHash/transactionHash are null while the
    /// operation is still pending in the mempool.
    /// </summary>
    public class UserOperationByHashResult
    {
        [JsonProperty(PropertyName = "userOperation")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("userOperation")]
#endif
        public UserOperation UserOperation { get; set; }

        [JsonProperty(PropertyName = "entryPoint")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("entryPoint")]
#endif
        public string EntryPoint { get; set; }

        [JsonProperty(PropertyName = "blockNumber")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("blockNumber")]
#endif
        public HexBigInteger BlockNumber { get; set; }

        [JsonProperty(PropertyName = "blockHash")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("blockHash")]
#endif
        public string BlockHash { get; set; }

        [JsonProperty(PropertyName = "transactionHash")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("transactionHash")]
#endif
        public string TransactionHash { get; set; }
    }
}
