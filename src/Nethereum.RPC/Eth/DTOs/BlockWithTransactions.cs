using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    /// <summary>
    ///     Block including transaction objects
    /// </summary>

    public class BlockWithTransactions : Block
    {
        /// <summary>
        ///     Array - Array of transaction objects
        /// </summary>
        [JsonProperty(PropertyName = "transactions")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("transactions")]
#endif
        public Transaction[] Transactions { get; set; }
    }
}