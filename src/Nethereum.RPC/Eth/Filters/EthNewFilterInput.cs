using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.Filters
{
    /// <summary>
    /// Object - The transaction object
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class NewFilterInput
    {

        public NewFilterInput()
        {
            FromBlock = new BlockParameter();
            ToBlock = new BlockParameter();
        }
       
        /// <summary>
        /// QUANTITY|TAG - (optional, default: "latest") Integer block number, or "latest" for the last mined block or "pending", "earliest" for not yet mined transactions.
        /// </summary>
        [JsonProperty(PropertyName = "fromBlock")]
        public BlockParameter FromBlock { get; set; }

        /// <summary>
        /// QUANTITY|TAG - (optional, default: "latest") Integer block number, or "latest" for the last mined block or "pending", "earliest" for not yet mined transactions.
        /// </summary>
        [JsonProperty(PropertyName = "toBlock")]
        public BlockParameter ToBlock { get; set; }

        /// <summary>
        /// address: DATA|Array, 20 Bytes - (optional) Contract address or a list of addresses from which logs should originate.
        /// </summary>
        [JsonProperty(PropertyName = "address")]
        public string[] Address { get; set; }

        /// <summary>
        /// topics: Array of DATA, - (optional) Array of 32 Bytes DATA topics. Topics are order-dependent. Each topic can also be an array of DATA with "or" options.
        /// </summary>
        /// <see cref="https://github.com/ethereum/wiki/wiki/Ethereum-Contract-ABI#events"/>
        [JsonProperty(PropertyName = "topics")]
        public object[] Topics { get; set; }
    }
}