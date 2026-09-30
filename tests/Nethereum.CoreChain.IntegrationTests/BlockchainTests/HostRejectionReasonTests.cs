using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.BlockchainTests
{
    public class HostRejectionReasonTests
    {
        private const string GasFloorFixture =
            "eip7976_increase_calldata_floor_cost/floor_boundary_exact_balance/below_amsterdam_floor_with_exact_balance_sender.json";

        private const string TheReasonItNames = "TransactionException.INTRINSIC_GAS_BELOW_FLOOR_GAS_COST";

        private const string ADifferentReason = "TransactionException.GAS_LIMIT_EXCEEDS_MAXIMUM";

        private readonly ITestOutputHelper _output;

        public HostRejectionReasonTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        [Trait("Category", "AmsterdamHostBlockchainTests")]
        public async Task Given_AFixtureExpectingAGasFloorRejection_When_TheBlockIsRejectedForThatReason_Then_TheTestPasses()
        {
            var run = await RunFixtureDemanding(TheReasonItNames).ConfigureAwait(false);

            Assert.Null(run.Verdict);
            Assert.Contains("Failed: 0", run.Report);
            Assert.Contains(TheReasonItNames + " matched by TransactionError.IntrinsicGasTooLow", run.Report);
        }

        [Fact]
        [Trait("Category", "AmsterdamHostBlockchainTests")]
        public async Task Given_AFixtureExpectingAGasFloorRejection_When_TheBlockIsRejectedForADifferentReason_Then_TheTestFails()
        {
            var run = await RunFixtureDemanding(ADifferentReason).ConfigureAwait(false);

            Assert.NotNull(run.Verdict);
            Assert.Contains("[rejectionReasonMismatch]", run.Report);

            Assert.Contains($"expected {ADifferentReason}, observed TransactionError.IntrinsicGasTooLow", run.Report);
            Assert.DoesNotContain("UNMAPPED", run.Report);
        }

        private sealed class FixtureRun
        {
            internal string Report { get; set; }

            internal Exception Verdict { get; set; }
        }

        private async Task<FixtureRun> RunFixtureDemanding(string expectException)
        {
            var fixture = Path.Combine(AmsterdamFixtureCorpus.Root ?? "", GasFloorFixture);
            Assert.True(File.Exists(fixture),
                $"The Amsterdam corpus fixture this test is built on is missing: {fixture}");

            var category = Path.Combine(Path.GetTempPath(), "ams-rej-02-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(category);
            try
            {
                File.WriteAllText(
                    Path.Combine(category, Path.GetFileName(fixture)),
                    File.ReadAllText(fixture).Replace(TheReasonItNames, expectException));

                var transcript = new Transcript(_output);
                var run = new FixtureRun();
                try
                {
                    await new HostBlockchainTestRunner(transcript)
                        .RunCategoryAsync(category, "Amsterdam").ConfigureAwait(false);
                }
                catch (Exception verdict)
                {
                    run.Verdict = verdict;
                }

                run.Report = transcript.ToString();
                return run;
            }
            finally
            {
                Directory.Delete(category, recursive: true);
            }
        }

        private sealed class Transcript : ITestOutputHelper
        {
            private readonly ITestOutputHelper _inner;
            private readonly List<string> _lines = new List<string>();

            internal Transcript(ITestOutputHelper inner) => _inner = inner;

            public void WriteLine(string message)
            {
                _lines.Add(message);
                _inner.WriteLine(message);
            }

            public void WriteLine(string format, params object[] args)
                => WriteLine(string.Format(format, args));

            public override string ToString() => string.Join(Environment.NewLine, _lines);
        }
    }
}
