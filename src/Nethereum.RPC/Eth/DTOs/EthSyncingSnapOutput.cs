using Nethereum.Hex.HexTypes;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    /// <summary>
    ///     Snap-sync progress object returned by eth_syncing while a snap bootstrap
    ///     is in progress. Field names match the snap progress object other clients
    ///     surface so existing tooling and dashboards read it unchanged.
    /// </summary>
    public class EthSyncingSnapOutput
    {
        [JsonProperty(PropertyName = "startingBlock")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("startingBlock")]
#endif
        public HexBigInteger StartingBlock { get; set; }

        [JsonProperty(PropertyName = "currentBlock")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("currentBlock")]
#endif
        public HexBigInteger CurrentBlock { get; set; }

        [JsonProperty(PropertyName = "highestBlock")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("highestBlock")]
#endif
        public HexBigInteger HighestBlock { get; set; }

        [JsonProperty(PropertyName = "syncedAccounts")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("syncedAccounts")]
#endif
        public HexBigInteger SyncedAccounts { get; set; }

        [JsonProperty(PropertyName = "syncedAccountBytes")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("syncedAccountBytes")]
#endif
        public HexBigInteger SyncedAccountBytes { get; set; }

        [JsonProperty(PropertyName = "syncedBytecodes")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("syncedBytecodes")]
#endif
        public HexBigInteger SyncedBytecodes { get; set; }

        [JsonProperty(PropertyName = "syncedBytecodeBytes")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("syncedBytecodeBytes")]
#endif
        public HexBigInteger SyncedBytecodeBytes { get; set; }

        [JsonProperty(PropertyName = "syncedStorage")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("syncedStorage")]
#endif
        public HexBigInteger SyncedStorage { get; set; }

        [JsonProperty(PropertyName = "syncedStorageBytes")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("syncedStorageBytes")]
#endif
        public HexBigInteger SyncedStorageBytes { get; set; }

        [JsonProperty(PropertyName = "healedTrienodes")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("healedTrienodes")]
#endif
        public HexBigInteger HealedTrienodes { get; set; }

        [JsonProperty(PropertyName = "healedTrienodeBytes")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("healedTrienodeBytes")]
#endif
        public HexBigInteger HealedTrienodeBytes { get; set; }

        [JsonProperty(PropertyName = "healedBytecodes")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("healedBytecodes")]
#endif
        public HexBigInteger HealedBytecodes { get; set; }

        [JsonProperty(PropertyName = "healingBytecode")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("healingBytecode")]
#endif
        public HexBigInteger HealingBytecode { get; set; }

        [JsonProperty(PropertyName = "healingTrienodes")]
#if NET6_0_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("healingTrienodes")]
#endif
        public HexBigInteger HealingTrienodes { get; set; }
    }
}
