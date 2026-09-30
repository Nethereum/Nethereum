using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nethereum.EEST.ConformanceRunner
{
    public static class StateTestLoader
    {
        public sealed class StateTest
        {
            [JsonPropertyName("env")] public TestEnv Env { get; set; } = new();
            [JsonPropertyName("pre")] public Dictionary<string, TestAccount> Pre { get; set; } = new();
            [JsonPropertyName("transaction")] public TestTransaction Transaction { get; set; } = new();
            [JsonPropertyName("post")] public Dictionary<string, List<PostEntry>> Post { get; set; } = new();
            [JsonPropertyName("config")] public TestConfig? Config { get; set; }
        }

        public sealed class TestConfig
        {
            [JsonPropertyName("blobSchedule")] public Dictionary<string, BlobScheduleEntry>? BlobSchedule { get; set; }
            [JsonPropertyName("chainid")] public string? ChainId { get; set; }
        }

        public sealed class BlobScheduleEntry
        {
            [JsonPropertyName("target")] public string? Target { get; set; }
            [JsonPropertyName("max")] public string? Max { get; set; }
            [JsonPropertyName("baseFeeUpdateFraction")] public string? BaseFeeUpdateFraction { get; set; }
        }

        public sealed class TestEnv
        {
            [JsonPropertyName("currentCoinbase")] public string? CurrentCoinbase { get; set; }
            [JsonPropertyName("currentDifficulty")] public string? CurrentDifficulty { get; set; }
            [JsonPropertyName("currentGasLimit")] public string? CurrentGasLimit { get; set; }
            [JsonPropertyName("currentNumber")] public string? CurrentNumber { get; set; }
            [JsonPropertyName("currentTimestamp")] public string? CurrentTimestamp { get; set; }
            [JsonPropertyName("currentBaseFee")] public string? CurrentBaseFee { get; set; }
            [JsonPropertyName("currentRandom")] public string? CurrentRandom { get; set; }
            [JsonPropertyName("currentExcessBlobGas")] public string? CurrentExcessBlobGas { get; set; }
            [JsonPropertyName("slotNumber")] public string? SlotNumber { get; set; }
        }

        public sealed class TestAccount
        {
            [JsonPropertyName("balance")] public string? Balance { get; set; }
            [JsonPropertyName("code")] public string? Code { get; set; }
            [JsonPropertyName("nonce")] public string? Nonce { get; set; }
            [JsonPropertyName("storage")] public Dictionary<string, string>? Storage { get; set; }
        }

        public sealed class TestTransaction
        {
            [JsonPropertyName("data")] public List<string>? Data { get; set; }
            [JsonPropertyName("gasLimit")] public List<string>? GasLimit { get; set; }
            [JsonPropertyName("gasPrice")] public string? GasPrice { get; set; }
            [JsonPropertyName("maxFeePerGas")] public string? MaxFeePerGas { get; set; }
            [JsonPropertyName("maxPriorityFeePerGas")] public string? MaxPriorityFeePerGas { get; set; }
            [JsonPropertyName("nonce")] public string? Nonce { get; set; }
            [JsonPropertyName("chainId")] public string? ChainId { get; set; }
            [JsonPropertyName("secretKey")] public string? SecretKey { get; set; }
            [JsonPropertyName("sender")] public string? Sender { get; set; }
            [JsonPropertyName("to")] public string? To { get; set; }
            [JsonPropertyName("value")] public List<string>? Value { get; set; }
            [JsonPropertyName("accessLists")] public List<List<AccessListItem>?>? AccessLists { get; set; }
            [JsonPropertyName("maxFeePerBlobGas")] public string? MaxFeePerBlobGas { get; set; }
            [JsonPropertyName("blobVersionedHashes")] public List<string>? BlobVersionedHashes { get; set; }
            [JsonPropertyName("authorizationList")] public List<AuthorizationListItem>? AuthorizationList { get; set; }
        }

        public sealed class AccessListItem
        {
            [JsonPropertyName("address")] public string Address { get; set; } = "";
            [JsonPropertyName("storageKeys")] public List<string>? StorageKeys { get; set; }
        }

        public sealed class AuthorizationListItem
        {
            [JsonPropertyName("chainId")] public string ChainId { get; set; } = "0x0";
            [JsonPropertyName("address")] public string Address { get; set; } = "";
            [JsonPropertyName("nonce")] public string Nonce { get; set; } = "0x0";
            [JsonPropertyName("v")] public string V { get; set; } = "0x0";
            [JsonPropertyName("r")] public string R { get; set; } = "0x0";
            [JsonPropertyName("s")] public string S { get; set; } = "0x0";
        }

        public sealed class PostEntry
        {
            [JsonPropertyName("indexes")] public PostIndexes Indexes { get; set; } = new();
            [JsonPropertyName("hash")] public string? Hash { get; set; }
            [JsonPropertyName("logs")] public string? Logs { get; set; }
            [JsonPropertyName("txbytes")] public string? TxBytes { get; set; }
            [JsonPropertyName("expectException")] public string? ExpectException { get; set; }
            [JsonPropertyName("state")] public Dictionary<string, TestAccount>? State { get; set; }
        }

        public sealed class PostIndexes
        {
            [JsonPropertyName("data")] public int Data { get; set; }
            [JsonPropertyName("gas")] public int Gas { get; set; }
            [JsonPropertyName("value")] public int Value { get; set; }
        }

        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = false
        };

        public static Dictionary<string, StateTest> LoadFromJson(string json) =>
            JsonSerializer.Deserialize<Dictionary<string, StateTest>>(json, Options)
            ?? new Dictionary<string, StateTest>();
    }
}
