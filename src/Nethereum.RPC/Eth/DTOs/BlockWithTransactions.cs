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
        public Transaction[] Transactions { get; set; }
    }
}