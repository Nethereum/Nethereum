using Newtonsoft.Json;
using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public class GeneralStateTest
    {
        [JsonProperty("_info")]
        public TestInfo Info { get; set; }

        [JsonProperty("env")]
        public TestEnv Env { get; set; }

        [JsonProperty("pre")]
        public Dictionary<string, TestAccount> Pre { get; set; }

        [JsonProperty("transaction")]
        public TestTransaction Transaction { get; set; }

        [JsonProperty("post")]
        public Dictionary<string, List<PostResult>> Post { get; set; }
    }

    public class TestInfo
    {
        [JsonProperty("comment")]
        public string Comment { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }
    }

    public class TestEnv
    {
        [JsonProperty("currentCoinbase")]
        public string CurrentCoinbase { get; set; }

        [JsonProperty("currentDifficulty")]
        public string CurrentDifficulty { get; set; }

        [JsonProperty("currentGasLimit")]
        public string CurrentGasLimit { get; set; }

        [JsonProperty("currentNumber")]
        public string CurrentNumber { get; set; }

        [JsonProperty("currentTimestamp")]
        public string CurrentTimestamp { get; set; }

        [JsonProperty("currentBaseFee")]
        public string CurrentBaseFee { get; set; }

        [JsonProperty("previousHash")]
        public string PreviousHash { get; set; }

        [JsonProperty("currentRandom")]
        public string CurrentRandom { get; set; }

        [JsonProperty("withdrawals")]
        public List<TestWithdrawal> Withdrawals { get; set; }

        [JsonProperty("currentExcessBlobGas")]
        public string CurrentExcessBlobGas { get; set; }

        [JsonProperty("parentBlobGasUsed")]
        public string ParentBlobGasUsed { get; set; }

        [JsonProperty("parentExcessBlobGas")]
        public string ParentExcessBlobGas { get; set; }
    }

    public class TestWithdrawal
    {
        [JsonProperty("index")]
        public string Index { get; set; }

        [JsonProperty("validatorIndex")]
        public string ValidatorIndex { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("amount")]
        public string Amount { get; set; }
    }

    public class TestAccount
    {
        [JsonProperty("balance")]
        public string Balance { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("nonce")]
        public string Nonce { get; set; }

        [JsonProperty("storage")]
        public Dictionary<string, string> Storage { get; set; }
    }

    public class TestTransaction
    {
        [JsonProperty("data")]
        public List<string> Data { get; set; }

        [JsonProperty("gasLimit")]
        public List<string> GasLimit { get; set; }

        [JsonProperty("gasPrice")]
        public string GasPrice { get; set; }

        [JsonProperty("maxFeePerGas")]
        public string MaxFeePerGas { get; set; }

        [JsonProperty("maxPriorityFeePerGas")]
        public string MaxPriorityFeePerGas { get; set; }

        [JsonProperty("nonce")]
        public string Nonce { get; set; }

        [JsonProperty("secretKey")]
        public string SecretKey { get; set; }

        [JsonProperty("sender")]
        public string Sender { get; set; }

        [JsonProperty("to")]
        public string To { get; set; }

        [JsonProperty("value")]
        public List<string> Value { get; set; }

        [JsonProperty("accessLists")]
        public List<List<AccessListItem>> AccessLists { get; set; }

        [JsonProperty("maxFeePerBlobGas")]
        public string MaxFeePerBlobGas { get; set; }

        [JsonProperty("blobVersionedHashes")]
        public List<string> BlobVersionedHashes { get; set; }
    }

    public class AccessListItem
    {
        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("storageKeys")]
        public List<string> StorageKeys { get; set; }
    }

    public class PostResult
    {
        [JsonProperty("indexes")]
        public PostIndexes Indexes { get; set; }

        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("logs")]
        public string Logs { get; set; }

        [JsonProperty("txbytes")]
        public string TxBytes { get; set; }

        [JsonProperty("expectException")]
        public string ExpectException { get; set; }

        [JsonProperty("state")]
        public Dictionary<string, TestAccount> State { get; set; }
    }

    public class PostIndexes
    {
        [JsonProperty("data")]
        public int Data { get; set; }

        [JsonProperty("gas")]
        public int Gas { get; set; }

        [JsonProperty("value")]
        public int Value { get; set; }
    }

    public enum Hardfork
    {
        Frontier,
        Homestead,
        TangerineWhistle,
        SpuriousDragon,
        Byzantium,
        Constantinople,
        ConstantinopleFix,
        Istanbul,
        MuirGlacier,
        Berlin,
        London,
        ArrowGlacier,
        GrayGlacier,
        Paris,
        Shanghai,
        Cancun,
        Prague
    }
}
