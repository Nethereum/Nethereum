using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.EVM;
using Newtonsoft.Json.Linq;

namespace Nethereum.CoreChain.RocksDB.UnitTests.Sync
{
    internal sealed class HiveForkSchedule : IChainActivations
    {
        public const string ForkEnvFileName = "forkenv.json";

        private static readonly (string Key, HardforkName Fork)[] TimestampForkKeys =
        {
            ("HIVE_SHANGHAI_TIMESTAMP",  HardforkName.Shanghai),
            ("HIVE_CANCUN_TIMESTAMP",    HardforkName.Cancun),
            ("HIVE_PRAGUE_TIMESTAMP",    HardforkName.Prague),
            ("HIVE_OSAKA_TIMESTAMP",     HardforkName.Osaka),
            ("HIVE_AMSTERDAM_TIMESTAMP", HardforkName.Amsterdam),
        };

        private readonly (ulong Timestamp, HardforkName Fork)[] _schedule;

        private HiveForkSchedule((ulong Timestamp, HardforkName Fork)[] schedule)
        {
            _schedule = schedule;
        }

        public IReadOnlyList<(ulong Timestamp, HardforkName Fork)> Activations => _schedule;

        public HardforkName ResolveAt(long blockNumber, ulong timestamp)
        {
            for (int i = _schedule.Length - 1; i > 0; i--)
                if (timestamp >= _schedule[i].Timestamp) return _schedule[i].Fork;
            return _schedule[0].Fork;
        }

        public static HiveForkSchedule Load(string testdataDir, Func<HardforkName, HardforkConfig> hardforkConfigFactory)
        {
            var path = System.IO.Path.Combine(testdataDir, ForkEnvFileName);
            return FromForkEnv(JObject.Parse(System.IO.File.ReadAllText(path)), hardforkConfigFactory, path);
        }

        public static HiveForkSchedule FromForkEnv(
            JObject forkEnv,
            Func<HardforkName, HardforkConfig> hardforkConfigFactory,
            string source = ForkEnvFileName)
        {
            RejectUnknownTimestampForks(forkEnv, source);
            RejectNonZeroBlockActivations(forkEnv, source);

            var schedule = ReadTimestampActivations(forkEnv);
            if (schedule.Length == 0)
                throw new InvalidOperationException(
                    $"{source} declares no timestamp-activated fork; the hivechain schedule is timestamp-driven " +
                    "and cannot be derived without at least HIVE_SHANGHAI_TIMESTAMP.");

            RejectNonGenesisFirstFork(schedule, source);
            RejectOutOfOrderActivations(schedule, source);
            RejectBlobParameterMismatch(forkEnv, schedule, hardforkConfigFactory, source);

            return new HiveForkSchedule(schedule);
        }

        private static (ulong Timestamp, HardforkName Fork)[] ReadTimestampActivations(JObject forkEnv)
            => TimestampForkKeys
                .Where(k => forkEnv[k.Key] != null)
                .Select(k => (Timestamp: ReadUInt64(forkEnv, k.Key), k.Fork))
                .ToArray();

        private static void RejectUnknownTimestampForks(JObject forkEnv, string source)
        {
            var known = TimestampForkKeys.Select(k => k.Key).ToHashSet(StringComparer.Ordinal);
            var unknown = forkEnv.Properties()
                .Select(p => p.Name)
                .Where(n => n.StartsWith("HIVE_", StringComparison.Ordinal)
                            && n.EndsWith("_TIMESTAMP", StringComparison.Ordinal)
                            && !known.Contains(n))
                .ToArray();
            if (unknown.Length > 0)
                throw new InvalidOperationException(
                    $"{source} activates fork(s) this fixture cannot express: {string.Join(", ", unknown)}. " +
                    "Map the key to a HardforkName in HiveForkSchedule.TimestampForkKeys — do not let the " +
                    "chain replay under the nearest fork we happen to know, that is a silent state-root divergence.");
        }

        private static void RejectNonZeroBlockActivations(JObject forkEnv, string source)
        {
            var late = forkEnv.Properties()
                .Where(p => (p.Name.StartsWith("HIVE_FORK_", StringComparison.Ordinal)
                             || p.Name == "HIVE_MERGE_BLOCK_ID")
                            && ReadUInt64(forkEnv, p.Name) != 0)
                .Select(p => $"{p.Name}={p.Value}")
                .ToArray();
            if (late.Length > 0)
                throw new InvalidOperationException(
                    $"{source} activates pre-merge fork(s) after block 0: {string.Join(", ", late)}. " +
                    "This fixture resolves forks purely by timestamp because the hivechain corpus has always been " +
                    "post-merge from genesis; a block-numbered ladder is needed before it can replay this chain.");
        }

        private static void RejectNonGenesisFirstFork((ulong Timestamp, HardforkName Fork)[] schedule, string source)
        {
            if (schedule[0].Timestamp != 0)
                throw new InvalidOperationException(
                    $"{source} activates its earliest fork {schedule[0].Fork} at timestamp {schedule[0].Timestamp}, " +
                    "not 0, so blocks before it fall outside the derived schedule.");
        }

        private static void RejectOutOfOrderActivations((ulong Timestamp, HardforkName Fork)[] schedule, string source)
        {
            for (int i = 1; i < schedule.Length; i++)
                if (schedule[i].Timestamp < schedule[i - 1].Timestamp)
                    throw new InvalidOperationException(
                        $"{source} activates {schedule[i].Fork} at timestamp {schedule[i].Timestamp}, before " +
                        $"{schedule[i - 1].Fork} at {schedule[i - 1].Timestamp}.");
        }

        private static void RejectBlobParameterMismatch(
            JObject forkEnv,
            (ulong Timestamp, HardforkName Fork)[] schedule,
            Func<HardforkName, HardforkConfig> hardforkConfigFactory,
            string source)
        {
            foreach (var (_, fork) in schedule)
            {
                var key = $"HIVE_{fork.ToString().ToUpperInvariant()}_BLOB_MAX";
                if (forkEnv[key] == null) continue;
                var expected = (int)ReadUInt64(forkEnv, key);
                var actual = hardforkConfigFactory(fork).MaxBlobsPerBlock;
                if (expected != actual)
                    throw new InvalidOperationException(
                        $"{source} sets {key}={expected} but our {fork} config allows {actual} blobs per block.");
            }
        }

        private static ulong ReadUInt64(JObject forkEnv, string key)
        {
            var raw = forkEnv[key].ToString();
            if (!ulong.TryParse(raw, out var value))
                throw new InvalidOperationException($"forkenv key {key} is not an integer: '{raw}'.");
            return value;
        }
    }
}
