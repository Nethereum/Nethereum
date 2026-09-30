using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Nethereum.CoreChain.Rpc
{
    public class AccountAccessDto
    {
        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("storageChanges")]
        public List<SlotChangesDto> StorageChanges { get; set; } = new List<SlotChangesDto>();

        [JsonPropertyName("storageReads")]
        public List<string> StorageReads { get; set; } = new List<string>();

        [JsonPropertyName("balanceChanges")]
        public List<BalanceChangeDto> BalanceChanges { get; set; } = new List<BalanceChangeDto>();

        [JsonPropertyName("nonceChanges")]
        public List<NonceChangeDto> NonceChanges { get; set; } = new List<NonceChangeDto>();

        [JsonPropertyName("codeChanges")]
        public List<CodeChangeDto> CodeChanges { get; set; } = new List<CodeChangeDto>();
    }

    public class SlotChangesDto
    {
        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("changes")]
        public List<StorageChangeDto> Changes { get; set; } = new List<StorageChangeDto>();
    }

    public class StorageChangeDto
    {
        [JsonPropertyName("index")]
        public string Index { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }
    }

    public class BalanceChangeDto
    {
        [JsonPropertyName("index")]
        public string Index { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }
    }

    public class NonceChangeDto
    {
        [JsonPropertyName("index")]
        public string Index { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }
    }

    public class CodeChangeDto
    {
        [JsonPropertyName("index")]
        public string Index { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; }
    }
}
