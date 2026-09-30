using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.EEST.ConformanceRunner
{
    [CollectionDefinition(Name)]
    public sealed class RpcCompatCollection : ICollectionFixture<HiveResultsSink>
    {
        public const string Name = "RpcCompat";
    }

    [Collection(RpcCompatCollection.Name)]
    public class RpcCompatConformanceTests
    {
        private readonly HiveResultsSink _sink;
        private readonly RpcCompatDriver _driver = RpcCompatDriver.Instance;

        private static readonly SemaphoreSlim ChainGate = new(1, 1);
        private static RpcReferenceChain _chain;

        public RpcCompatConformanceTests(HiveResultsSink sink)
        {
            _sink = sink;
            _sink.EnsureSuite(_driver);
        }

        private static async Task<RpcReferenceChain> GetChainAsync()
        {
            if (_chain != null) return _chain;
            await ChainGate.WaitAsync().ConfigureAwait(false);
            try
            {
                _chain ??= await RpcReferenceChain.CreateAsync().ConfigureAwait(false);
                return _chain;
            }
            finally
            {
                ChainGate.Release();
            }
        }

        public static TheoryData<string> Cases() => FixtureCatalog.RpcCompatCases();

        [Theory]
        [MemberData(nameof(Cases))]
        public async Task Vector(string file)
        {
            if (file == FixtureCatalog.MissingCacheSentinel)
            {
                Assert.Fail(
                    $"ethereum/execution-apis vectors not found at '{FixtureProvisioning.ExecutionApisTestsRoot}'. " +
                    $"Provision execution-apis {FixtureProvisioning.ExecutionApisTag} (see run-acceptance.sh) before this gate.");
                return;
            }

            if (file == FixtureCatalog.NoFixturesSentinel)
            {
                Assert.Fail(
                    $"execution-apis tests root at '{FixtureProvisioning.ExecutionApisTestsRoot}' exists but contains " +
                    "zero *.io files - a category discovering 0 files must fail loudly, never be skipped.");
                return;
            }

            var start = DateTimeOffset.UtcNow;
            var caseId = RelativePath(file);
            var chain = await GetChainAsync();

            var exchanges = RpcCompatVectorLoader.Load(file);
            if (exchanges.Count == 0)
            {
                var end0 = DateTimeOffset.UtcNow;
                _sink.Record(caseId, caseId, false, "[malformedVector] no request/response exchange in file", start, end0);
                Assert.Fail($"No request/response exchange in {file}.");
                return;
            }

            ConformanceCaseResult result = ConformanceCaseResult.Ok();
            foreach (var exchange in exchanges)
            {
                try
                {
                    result = await _driver.RunAsync(chain, exchange);
                }
                catch (Exception ex)
                {
                    result = ConformanceCaseResult.Fail("harnessException", $"{ex.GetType().Name}: {ex.Message}");
                }

                if (!result.Success) break;
            }

            var end = DateTimeOffset.UtcNow;
            var details = result.Success ? "" : $"[{result.Kind}] {result.Detail}";
            _sink.Record(caseId, caseId, result.Success, details, start, end);

            Assert.True(result.Success, details);
        }

        private static string RelativePath(string file)
        {
            var root = FixtureCatalog.RepoRoot;
            if (root == null) return file;
            return Path.GetRelativePath(root, file).Replace('\\', '/');
        }
    }
}
