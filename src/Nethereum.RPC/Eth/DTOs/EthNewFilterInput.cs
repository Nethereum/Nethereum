using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    /// <summary>
    ///     Object - The transaction object
    /// </summary>
    public class NewFilterInput
    {
        public NewFilterInput()
        {
            FromBlock = new BlockParameter();
            ToBlock = new BlockParameter();
        }

        /// <summary>
        ///     QUANTITY|TAG - (optional, default: "latest") Integer block number, or "latest" for the last mined block or
        ///     "pending", "earliest" for not yet mined transactions.
        /// </summary>
        [JsonProperty(PropertyName = "fromBlock")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("fromBlock")]
#endif
        public BlockParameter FromBlock { get; set; }

        /// <summary>
        ///     QUANTITY|TAG - (optional, default: "latest") Integer block number, or "latest" for the last mined block or
        ///     "pending", "earliest" for not yet mined transactions.
        /// </summary>
        [JsonProperty(PropertyName = "toBlock")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("toBlock")]
#endif
        public BlockParameter ToBlock { get; set; }

        /// <summary>
        ///     address: DATA|Array, 20 Bytes - (optional) Contract address or a list of addresses from which logs should
        ///     originate.
        /// </summary>
        [JsonProperty(PropertyName = "address")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("address")]
#endif
        public string[] Address { get; set; }

        /// <summary>
        ///     topics: Array of DATA, - (optional) Array of 32 Bytes DATA topics. Topics are order-dependent. Each topic can also
        ///     be an array of DATA with "or" options.
        /// </summary>
        /// <see cref="https://github.com/ethereum/wiki/wiki/Ethereum-Contract-ABI#events" />
        [JsonProperty(PropertyName = "topics")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("topics")]
#endif
        public object[] Topics { get; set; }
    }
}