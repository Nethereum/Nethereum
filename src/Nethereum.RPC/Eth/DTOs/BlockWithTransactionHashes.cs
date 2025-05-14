using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    /// <summary>
    ///     Block including just the transaction hashes
    /// </summary>
    public class BlockWithTransactionHashes : Block
    {
        /// <summary>
        ///     Array - Array of transaction hashes
        /// </summary>
        [JsonProperty(PropertyName = "transactions")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("transactions")]
#endif
        public string[] TransactionHashes { get; set; }
    }
}