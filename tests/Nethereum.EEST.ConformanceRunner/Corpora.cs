using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nethereum.EEST.Conformance;
using Nethereum.Testing;
using Xunit;

namespace Nethereum.EEST.ConformanceRunner
{
    public static class Corpora
    {
        public const string MissingCacheSentinel = "__MISSING_FIXTURE_CACHE__";
        public const string NoFixturesSentinel = "__NO_FIXTURES_DISCOVERED__";
        public const string FilteredOutSentinel = "__NO_CASE_MATCHES_CONFORMANCE_CASE__";

        public static readonly string? RepoRoot = TestEnvironment.RepoRoot;

        public static readonly ICorpusSource Blockchain = Eest(CaseKind.Blockchain, "blockchain_tests");
        public static readonly ICorpusSource BlockchainEngine = Eest(CaseKind.BlockchainEngine, "blockchain_tests_engine");
        public static readonly ICorpusSource State = Eest(CaseKind.State, "state_tests");
        public static readonly ICorpusSource Transaction = Eest(CaseKind.Transaction, "transaction_tests");
        public static readonly ICorpusSource RpcCompat = new ExecutionApisSource(FixtureProvisioning.ExecutionApisTestsRoot);
        public static readonly ICorpusSource AmsterdamState =
            new EestFixtureSource(CaseKind.State, Path.Combine(TestEnvironment.ExternalPath("EestAmsterdam"), "state_tests", "amsterdam"), "amsterdam/state_tests");
        public static readonly ICorpusSource AmsterdamBlockchain =
            new EestFixtureSource(CaseKind.Blockchain, Path.Combine(TestEnvironment.ExternalPath("EestAmsterdam"), "blockchain_tests", "amsterdam"), "amsterdam/blockchain_tests");
        public static readonly ICorpusSource AmsterdamBlockchainEngine =
            new EestFixtureSource(CaseKind.BlockchainEngine, Path.Combine(TestEnvironment.ExternalPath("EestAmsterdam"), "blockchain_tests_engine", "amsterdam"), "amsterdam/blockchain_tests_engine");
        public static readonly ICorpusSource FusakaDevnet5Blockchain =
            new EestFixtureSource(CaseKind.Blockchain, Path.Combine(TestEnvironment.ExternalPath("EestFusakaDevnet5"), "blockchain_tests", "osaka"), "fusaka-devnet-5/blockchain_tests");
        public static readonly ICorpusSource LegacyCancunState = Legacy("Cancun");
        public static readonly ICorpusSource LegacyConstantinopleState = Legacy("Constantinople");
        public static readonly ICorpusSource EthereumTestsState =
            new EestFixtureSource(CaseKind.State, Path.Combine(TestEnvironment.ExternalPath("EthereumTests"), "GeneralStateTests"), "ethereum-tests/GeneralStateTests");
        public static readonly ICorpusSource LegacyConstantinopleInvalidBlocks = LegacyBlockchain("Constantinople", "InvalidBlocks");
        public static readonly ICorpusSource LegacyConstantinopleValidBlocks = LegacyBlockchain("Constantinople", "ValidBlocks");
        public static readonly ICorpusSource LegacyCancunInvalidBlocks = LegacyBlockchain("Cancun", "InvalidBlocks");
        public static readonly ICorpusSource LegacyCancunValidBlocks = LegacyBlockchain("Cancun", "ValidBlocks");
        public static readonly ICorpusSource EthereumTestsBlockchain =
            new EestFixtureSource(CaseKind.Blockchain, Path.Combine(TestEnvironment.ExternalPath("EthereumTests"), "BlockchainTests"), "ethereum-tests/BlockchainTests");

        public static TheoryData<string> TheoryCases(ICorpusSource source)
        {
            var data = new TheoryData<string>();

            if (!source.IsPresent)
            {
                data.Add(MissingCacheSentinel);
                return data;
            }

            if (source.Cases.Count == 0)
            {
                data.Add(NoFixturesSentinel);
                return data;
            }

            foreach (var c in Selected(source))
                data.Add(c.Id);

            if (!data.Any())
                data.Add(FilteredOutSentinel);

            return data;
        }

        public static IEnumerable<ConformanceCase> Selected(ICorpusSource source)
        {
            var filter = ConformanceSettings.Case;
            return string.IsNullOrEmpty(filter)
                ? source.Cases
                : source.Cases.Where(c => c.Id.Contains(filter, StringComparison.Ordinal));
        }

        public static string HiveCaseId(ConformanceCase c) =>
            c.Kind == CaseKind.RpcCompat ? RelativePath(c.File) : $"{RelativePath(c.File)}::{c.DisplayName}";

        public static string RelativePath(string file) =>
            RepoRoot == null ? file : Path.GetRelativePath(RepoRoot, file).Replace('\\', '/');

        private static ICorpusSource Eest(CaseKind kind, string directory) =>
            new EestFixtureSource(kind, Path.Combine(TestEnvironment.ExternalPath("EestStable"), directory));

        private static ICorpusSource LegacyBlockchain(string branch, string category) =>
            new EestFixtureSource(CaseKind.Blockchain, Path.Combine(TestEnvironment.ExternalPath("LegacyTests"), branch, "BlockchainTests", category), $"legacytests/{branch}/BlockchainTests/{category}");

        private static ICorpusSource Legacy(string branch) =>
            new EestFixtureSource(CaseKind.State, Path.Combine(TestEnvironment.ExternalPath("LegacyTests"), branch, "GeneralStateTests"), $"legacytests/{branch}");
    }
}
