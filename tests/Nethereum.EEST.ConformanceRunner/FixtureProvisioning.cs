using System.IO;
using Nethereum.Testing;

namespace Nethereum.EEST.ConformanceRunner
{
    public static class FixtureProvisioning
    {
        public const string EestFixturesVersion = "v20.0.2";
        public const string ExecutionApisTag = "v1.0.0-beta.7";

        public static readonly string ExecutionApisTestsRoot =
            Path.Combine(TestEnvironment.ExternalPath("ExecutionApis"), "tests");
    }
}
