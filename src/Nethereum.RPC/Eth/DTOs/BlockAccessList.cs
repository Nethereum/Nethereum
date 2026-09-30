using System.Collections.Generic;
using Newtonsoft.Json;

namespace Nethereum.RPC.Eth.DTOs
{
    public class AccountAccess
    {
        [JsonProperty(PropertyName = "address")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("address")]
#endif
        public string Address { get; set; }

        [JsonProperty(PropertyName = "storageChanges")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("storageChanges")]
#endif
        public List<SlotChanges> StorageChanges { get; set; } = new List<SlotChanges>();

        [JsonProperty(PropertyName = "storageReads")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("storageReads")]
#endif
        public List<string> StorageReads { get; set; } = new List<string>();

        [JsonProperty(PropertyName = "balanceChanges")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("balanceChanges")]
#endif
        public List<BalanceChange> BalanceChanges { get; set; } = new List<BalanceChange>();

        [JsonProperty(PropertyName = "nonceChanges")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("nonceChanges")]
#endif
        public List<NonceChange> NonceChanges { get; set; } = new List<NonceChange>();

        [JsonProperty(PropertyName = "codeChanges")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("codeChanges")]
#endif
        public List<CodeChange> CodeChanges { get; set; } = new List<CodeChange>();
    }

    public class SlotChanges
    {
        [JsonProperty(PropertyName = "key")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("key")]
#endif
        public string Key { get; set; }

        [JsonProperty(PropertyName = "changes")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("changes")]
#endif
        public List<StorageChange> Changes { get; set; } = new List<StorageChange>();
    }

    public class StorageChange
    {
        [JsonProperty(PropertyName = "index")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("index")]
#endif
        public string Index { get; set; }

        [JsonProperty(PropertyName = "value")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("value")]
#endif
        public string Value { get; set; }
    }

    public class BalanceChange
    {
        [JsonProperty(PropertyName = "index")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("index")]
#endif
        public string Index { get; set; }

        [JsonProperty(PropertyName = "value")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("value")]
#endif
        public string Value { get; set; }
    }

    public class NonceChange
    {
        [JsonProperty(PropertyName = "index")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("index")]
#endif
        public string Index { get; set; }

        [JsonProperty(PropertyName = "value")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("value")]
#endif
        public string Value { get; set; }
    }

    public class CodeChange
    {
        [JsonProperty(PropertyName = "index")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("index")]
#endif
        public string Index { get; set; }

        [JsonProperty(PropertyName = "code")]
#if NET6_0_OR_GREATER
[System.Text.Json.Serialization.JsonPropertyName("code")]
#endif
        public string Code { get; set; }
    }
}
