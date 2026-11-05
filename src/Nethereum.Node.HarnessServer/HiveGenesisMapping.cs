using Newtonsoft.Json.Linq;

namespace Nethereum.Node.HarnessServer
{
    public static class HiveGenesisMapping
    {
        public const string TargetGasLimitVariable = "HIVE_TARGET_GAS_LIMIT";
        public const long DefaultTargetGasLimit = 60_000_000;

        private const string DefaultDepositContract = "0x00000000219ab540356cBB839Cbe05303d7705Fa";
        private const string DefaultTerminalTotalDifficulty = "9223372036854775807";

        private static readonly (string Key, string Variable)[] ForkBlocks =
        {
            ("homesteadBlock", "HIVE_FORK_HOMESTEAD"),
            ("daoForkBlock", "HIVE_FORK_DAO_BLOCK"),
            ("eip150Block", "HIVE_FORK_TANGERINE"),
            ("eip155Block", "HIVE_FORK_SPURIOUS"),
            ("eip158Block", "HIVE_FORK_SPURIOUS"),
            ("byzantiumBlock", "HIVE_FORK_BYZANTIUM"),
            ("constantinopleBlock", "HIVE_FORK_CONSTANTINOPLE"),
            ("petersburgBlock", "HIVE_FORK_PETERSBURG"),
            ("istanbulBlock", "HIVE_FORK_ISTANBUL"),
            ("muirGlacierBlock", "HIVE_FORK_MUIR_GLACIER"),
            ("berlinBlock", "HIVE_FORK_BERLIN"),
            ("londonBlock", "HIVE_FORK_LONDON"),
            ("arrowGlacierBlock", "HIVE_FORK_ARROW_GLACIER"),
            ("grayGlacierBlock", "HIVE_FORK_GRAY_GLACIER"),
            ("mergeNetsplitBlock", "HIVE_MERGE_BLOCK_ID"),
        };

        private static readonly (string Key, string Variable)[] ForkTimes =
        {
            ("shanghaiTime", "HIVE_SHANGHAI_TIMESTAMP"),
            ("cancunTime", "HIVE_CANCUN_TIMESTAMP"),
            ("pragueTime", "HIVE_PRAGUE_TIMESTAMP"),
            ("osakaTime", "HIVE_OSAKA_TIMESTAMP"),
            ("amsterdamTime", "HIVE_AMSTERDAM_TIMESTAMP"),
            ("bogotaTime", "HIVE_BOGOTA_TIMESTAMP"),
        };

        public static bool IsHiveEnvironment(IReadOnlyDictionary<string, string> environment) =>
            environment.Keys.Any(k => k.StartsWith("HIVE_", StringComparison.Ordinal));

        public static JObject Apply(JObject genesis, IReadOnlyDictionary<string, string> environment)
        {
            var mapped = (JObject)genesis.DeepClone();
            mapped["config"] = BuildConfig(environment);
            return mapped;
        }

        public static long TargetGasLimit(IReadOnlyDictionary<string, string> environment) =>
            Get(environment, TargetGasLimitVariable) is { } value ? long.Parse(value) : DefaultTargetGasLimit;

        private static JObject BuildConfig(IReadOnlyDictionary<string, string> env)
        {
            var clique = Get(env, "HIVE_CLIQUE_PERIOD");
            var config = new JObject
            {
                ["ethash"] = clique == null ? new JObject() : null,
                ["clique"] = clique == null ? null : new JObject { ["period"] = Number(clique) },
                ["chainId"] = Number(Get(env, "HIVE_CHAIN_ID")),
                ["daoForkSupport"] = Get(env, "HIVE_FORK_DAO_VOTE") == "1",
                ["eip150Hash"] = Get(env, "HIVE_FORK_TANGERINE_HASH"),
            };

            foreach (var (key, variable) in ForkBlocks) config[key] = Number(Get(env, variable));

            config["terminalTotalDifficulty"] = Number(Get(env, "HIVE_TERMINAL_TOTAL_DIFFICULTY") ?? DefaultTerminalTotalDifficulty);

            foreach (var (key, variable) in ForkTimes) config[key] = Number(Get(env, variable));

            config["terminalTotalDifficultyPassed"] = true;

            var blobSchedule = new JObject
            {
                ["cancun"] = BlobParameters(env, "CANCUN", 3, 6, 3338477),
                ["prague"] = BlobParameters(env, "PRAGUE", 6, 9, 5007716),
            };
            AddBpo(config, blobSchedule, env, 1, 10, 15, 8346193);
            AddBpo(config, blobSchedule, env, 2, 14, 21, 11684671);
            AddBpo(config, blobSchedule, env, 3, 9, 14, 8832827);
            AddBpo(config, blobSchedule, env, 4, 9, 14, 8832827);
            AddBpo(config, blobSchedule, env, 5, 9, 14, 8832827);
            config["blobSchedule"] = blobSchedule;

            config["depositContractAddress"] = Get(env, "HIVE_DEPOSIT_CONTRACT_ADDRESS") ?? DefaultDepositContract;

            return RemoveEmpty(config);
        }

        private static void AddBpo(
            JObject config, JObject blobSchedule, IReadOnlyDictionary<string, string> env,
            int index, long target, long max, long fraction)
        {
            var timestamp = Get(env, $"HIVE_BPO{index}_TIMESTAMP");
            if (timestamp == null) return;
            config[$"bpo{index}Time"] = Number(timestamp);
            blobSchedule[$"bpo{index}"] = BlobParameters(env, $"BPO{index}", target, max, fraction);
        }

        private static JObject BlobParameters(
            IReadOnlyDictionary<string, string> env, string fork, long target, long max, long fraction) =>
            new JObject
            {
                ["target"] = Number(Get(env, $"HIVE_{fork}_BLOB_TARGET")) ?? new JValue(target),
                ["max"] = Number(Get(env, $"HIVE_{fork}_BLOB_MAX")) ?? new JValue(max),
                ["baseFeeUpdateFraction"] = Number(Get(env, $"HIVE_{fork}_BLOB_BASE_FEE_UPDATE_FRACTION")) ?? new JValue(fraction),
            };

        private static string? Get(IReadOnlyDictionary<string, string> env, string name) =>
            env.TryGetValue(name, out var value) && value.Length > 0 ? value : null;

        private static JToken? Number(string? text) =>
            text == null ? null : new JValue(long.Parse(text, System.Globalization.CultureInfo.InvariantCulture));

        private static JObject RemoveEmpty(JObject node)
        {
            foreach (var property in node.Properties().ToList())
            {
                if (property.Value is JObject child) RemoveEmpty(child);
                var value = property.Value;
                if (value.Type == JTokenType.Null
                    || (value.Type == JTokenType.String && (string)value! == "")
                    || (value is JArray array && array.Count == 0))
                    property.Remove();
            }
            return node;
        }
    }
}
