using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Nethereum.EEST.ConformanceRunner
{
    public static class FixtureCatalog
    {
        public const string MissingCacheSentinel = "__MISSING_FIXTURE_CACHE__";
        public const string NoFixturesSentinel = "__NO_FIXTURES_DISCOVERED__";

        public static readonly string? RepoRoot = FindRepoRoot();

        public static readonly string BlockchainTestsRoot =
            Path.Combine(RepoRoot ?? Directory.GetCurrentDirectory(),
                "external", "execution-spec-tests", "fixtures", "blockchain_tests");

        public static readonly string StateTestsRoot =
            Path.Combine(RepoRoot ?? Directory.GetCurrentDirectory(),
                "external", "execution-spec-tests", "fixtures", "state_tests");

        public static readonly string BlockchainTestsEngineRoot =
            Path.Combine(RepoRoot ?? Directory.GetCurrentDirectory(),
                "external", "execution-spec-tests", "fixtures", "blockchain_tests_engine");

        public static readonly string TransactionTestsRoot =
            Path.Combine(RepoRoot ?? Directory.GetCurrentDirectory(),
                "external", "execution-spec-tests", "fixtures", "transaction_tests");

        public static bool CacheIsPresent => Directory.Exists(BlockchainTestsRoot);

        public static bool StateTestsCacheIsPresent => Directory.Exists(StateTestsRoot);

        public static bool BlockchainTestsEngineCacheIsPresent => Directory.Exists(BlockchainTestsEngineRoot);

        public static bool TransactionTestsCacheIsPresent => Directory.Exists(TransactionTestsRoot);

        private static readonly Lazy<List<string>> _files = new(() =>
            CacheIsPresent
                ? Directory.GetFiles(BlockchainTestsRoot, "*.json", SearchOption.AllDirectories)
                    .OrderBy(f => f, StringComparer.Ordinal)
                    .ToList()
                : new List<string>());

        private static readonly Lazy<List<string>> _stateTestFiles = new(() =>
            StateTestsCacheIsPresent
                ? Directory.GetFiles(StateTestsRoot, "*.json", SearchOption.AllDirectories)
                    .OrderBy(f => f, StringComparer.Ordinal)
                    .ToList()
                : new List<string>());

        private static readonly Lazy<List<string>> _engineFiles = new(() =>
            BlockchainTestsEngineCacheIsPresent
                ? Directory.GetFiles(BlockchainTestsEngineRoot, "*.json", SearchOption.AllDirectories)
                    .OrderBy(f => f, StringComparer.Ordinal)
                    .ToList()
                : new List<string>());

        private static readonly Lazy<List<string>> _transactionTestFiles = new(() =>
            TransactionTestsCacheIsPresent
                ? Directory.GetFiles(TransactionTestsRoot, "*.json", SearchOption.AllDirectories)
                    .OrderBy(f => f, StringComparer.Ordinal)
                    .ToList()
                : new List<string>());

        public static IReadOnlyList<string> AllFixtureFiles => _files.Value;

        public static IReadOnlyList<string> AllStateTestFiles => _stateTestFiles.Value;

        public static IReadOnlyList<string> AllEngineFixtureFiles => _engineFiles.Value;

        public static IReadOnlyList<string> AllTransactionTestFiles => _transactionTestFiles.Value;

        public static TheoryData<string, string> AllCases()
        {
            var data = new TheoryData<string, string>();

            if (!CacheIsPresent)
            {
                data.Add(MissingCacheSentinel, BlockchainTestsRoot);
                return data;
            }

            foreach (var file in AllFixtureFiles)
            {
                foreach (var name in ReadTopLevelTestNames(file))
                    data.Add(file, name);
            }

            if (data.Count() == 0)
                data.Add(NoFixturesSentinel, BlockchainTestsRoot);

            return data;
        }

        public static int CountAllCases() =>
            AllFixtureFiles.Sum(f => ReadTopLevelTestNames(f).Count);

        public static TheoryData<string, string> EngineCases()
        {
            var data = new TheoryData<string, string>();

            if (!BlockchainTestsEngineCacheIsPresent)
            {
                data.Add(MissingCacheSentinel, BlockchainTestsEngineRoot);
                return data;
            }

            foreach (var file in AllEngineFixtureFiles)
            {
                foreach (var name in ReadTopLevelTestNames(file))
                    data.Add(file, name);
            }

            if (data.Count() == 0)
                data.Add(NoFixturesSentinel, BlockchainTestsEngineRoot);

            return data;
        }

        public static TheoryData<string, string> TransactionCases()
        {
            var data = new TheoryData<string, string>();

            if (!TransactionTestsCacheIsPresent)
            {
                data.Add(MissingCacheSentinel, TransactionTestsRoot);
                return data;
            }

            foreach (var file in AllTransactionTestFiles)
            {
                foreach (var name in ReadTopLevelTestNames(file))
                    data.Add(file, name);
            }

            if (data.Count() == 0)
                data.Add(NoFixturesSentinel, TransactionTestsRoot);

            return data;
        }

        public static TheoryData<string> RpcCompatCases()
        {
            var data = new TheoryData<string>();

            if (!FixtureProvisioning.ExecutionApisCacheIsPresent)
            {
                data.Add(MissingCacheSentinel);
                return data;
            }

            var files = Directory.GetFiles(FixtureProvisioning.ExecutionApisTestsRoot, "*.io", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();

            foreach (var file in files)
                data.Add(file);

            if (data.Count() == 0)
                data.Add(NoFixturesSentinel);

            return data;
        }

        public static List<(string File, string Name)> EngineCaseList()
        {
            var list = new List<(string, string)>();
            foreach (var file in AllEngineFixtureFiles)
                foreach (var name in ReadTopLevelTestNames(file))
                    list.Add((file, name));
            return list;
        }

        public static int CountAllEngineCases() =>
            AllEngineFixtureFiles.Sum(f => ReadTopLevelTestNames(f).Count);

        public static int CountAllTransactionCases() =>
            AllTransactionTestFiles.Sum(f => ReadTopLevelTestNames(f).Count);

        public static TheoryData<string, string, string, int, int, int> AllStateTestCases()
        {
            var data = new TheoryData<string, string, string, int, int, int>();

            if (!StateTestsCacheIsPresent)
            {
                data.Add(MissingCacheSentinel, StateTestsRoot, "", 0, 0, 0);
                return data;
            }

            foreach (var file in AllStateTestFiles)
            {
                foreach (var (name, fork, dataIdx, gasIdx, valueIdx) in ReadStateTestCaseKeys(file))
                    data.Add(file, name, fork, dataIdx, gasIdx, valueIdx);
            }

            if (data.Count() == 0)
                data.Add(NoFixturesSentinel, StateTestsRoot, "", 0, 0, 0);

            return data;
        }

        public static int CountAllStateTestCases() =>
            AllStateTestFiles.Sum(f => ReadStateTestCaseKeys(f).Count);

        public static IReadOnlyList<string> StateTestTopLevelCategories() =>
            StateTestsCacheIsPresent
                ? Directory.GetDirectories(StateTestsRoot).OrderBy(d => d, StringComparer.Ordinal).ToList()
                : Array.Empty<string>();

        private static List<(string Name, string Fork, int Data, int Gas, int Value)> ReadStateTestCaseKeys(string file)
        {
            var results = new List<(string, string, int, int, int)>();

            using var stream = File.OpenRead(file);
            using var doc = JsonDocument.Parse(stream);

            foreach (var testProp in doc.RootElement.EnumerateObject())
            {
                if (!testProp.Value.TryGetProperty("post", out var post)) continue;

                foreach (var forkProp in post.EnumerateObject())
                {
                    foreach (var entry in forkProp.Value.EnumerateArray())
                    {
                        int d = 0, g = 0, v = 0;
                        if (entry.TryGetProperty("indexes", out var idx))
                        {
                            if (idx.TryGetProperty("data", out var dv) && dv.ValueKind == JsonValueKind.Number) d = dv.GetInt32();
                            if (idx.TryGetProperty("gas", out var gv) && gv.ValueKind == JsonValueKind.Number) g = gv.GetInt32();
                            if (idx.TryGetProperty("value", out var vv) && vv.ValueKind == JsonValueKind.Number) v = vv.GetInt32();
                        }

                        results.Add((testProp.Name, forkProp.Name, d, g, v));
                    }
                }
            }

            return results;
        }

        public static IReadOnlyList<string> TopLevelCategories() =>
            CacheIsPresent
                ? Directory.GetDirectories(BlockchainTestsRoot).OrderBy(d => d, StringComparer.Ordinal).ToList()
                : Array.Empty<string>();

        private static List<string> ReadTopLevelTestNames(string file)
        {
            var names = new List<string>();
            var bytes = File.ReadAllBytes(file);
            var reader = new Utf8JsonReader(bytes, isFinalBlock: true, state: default);

            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                return names;

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName) continue;

                names.Add(reader.GetString() ?? "");
                reader.Skip();
            }

            return names;
        }

        private static string? FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Nethereum.slnx")) ||
                    File.Exists(Path.Combine(dir.FullName, "Nethereum.sln")))
                    return dir.FullName;
                dir = dir.Parent;
            }

            dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Nethereum.slnx")) ||
                    File.Exists(Path.Combine(dir.FullName, "Nethereum.sln")))
                    return dir.FullName;
                dir = dir.Parent;
            }

            return null;
        }
    }
}
