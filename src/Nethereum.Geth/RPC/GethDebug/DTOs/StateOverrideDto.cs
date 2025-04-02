using System.Collections.Generic;
using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;
using Org.BouncyCastle.Math;

namespace Nethereum.Geth.RPC.Debug.DTOs
{
    /// <summary>
    /// The state override set is an optional address-to-state mapping, used in eth_call and eth_simulateV1, where each
    /// entry specifies some state to be ephemerally overridden prior to executing the call
    /// </summary>
    public class StateOverrideDto
    {
        /// <summary>
        /// Fake balance to set for the account before executing the call.
        /// </summary>
        [JsonProperty("balance")]
        public HexBigInteger Balance { get; set; }

        /// <summary>
        /// Fake nonce to set for the account before executing the call.
        /// </summary>
        [JsonProperty("nonce")]
        public string Nonce { get; set; }

        /// <summary>
        /// Fake EVM bytecode to inject into the account before executing the call.
        /// </summary>
        [JsonProperty("code")]
        public string Code { get; set; }

        /// <summary>
        /// Fake key-value mapping to override all slots in the account storage before executing the call.
        /// </summary>
        [JsonProperty("state")]
        public Dictionary<string, string> State { get; set; }

        /// <summary>
        /// Fake key-value mapping to override individual slots in the account storage before executing the call.
        /// </summary>
        [JsonProperty("stateDiff")]
        public Dictionary<string, string> StateDiff { get; set; }

        /// <summary>
        /// Moves precompile to given address
        /// </summary>
        [JsonProperty("movePrecompileToAddress")]
        public string MovePrecompileToAddress { get; set; }
    }
}