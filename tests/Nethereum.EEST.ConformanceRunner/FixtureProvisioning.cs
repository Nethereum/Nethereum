using System.IO;

namespace Nethereum.EEST.ConformanceRunner
{
    public static class FixtureProvisioning
    {
        public const string EestFixturesVersion = "v5.4.0";
        public const string ExecutionApisTag = "v1.0.0-beta.7";

        public static readonly string ExecutionApisTestsRoot =
            Path.Combine(FixtureCatalog.RepoRoot ?? Directory.GetCurrentDirectory(),
                "external", "execution-apis", "tests");

        public static bool ExecutionApisCacheIsPresent =>
            File.Exists(Path.Combine(ExecutionApisTestsRoot, "chain.rlp"));

        public static bool AllPresent =>
            FixtureCatalog.CacheIsPresent
            && FixtureCatalog.StateTestsCacheIsPresent
            && FixtureCatalog.BlockchainTestsEngineCacheIsPresent
            && FixtureCatalog.TransactionTestsCacheIsPresent
            && ExecutionApisCacheIsPresent;
    }
}
