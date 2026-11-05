using System.Linq;
using Nethereum.Model.UnitTests.Oracle;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests.GethReference
{
    [Trait("Requires", "Externals")]
    [Collection("GethReference")]
    public class GethReferenceDecodeDifferentialTests
    {
        private readonly ITestOutputHelper _output;

        public GethReferenceDecodeDifferentialTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Given_EveryPeerFacingDecoder_When_RunOnCorporaGeneratedObjectsAndStructuralMutants_Then_NethereumAgreesWithGethV1_17_4()
        {
            var limit = ReferenceRun.ConfiguredLimit();
            var inputs = ReferenceRun.ConfiguredInputs();
            var corpus = DifferentialCorpus.Load(InputPlanner.BoundsFor(inputs));
            var planner = new InputPlanner(inputs, ReferenceRun.ConfiguredSeed(), corpus);
            var allowed = ReferenceRun.LoadAllowlist().Select(e => e.Id).ToHashSet();

            var run = new ReferenceRun();
            using (var geth = new GethReferenceProcess(limit))
                run.Execute(planner.Plan(), geth, allowed);

            var folder = run.WriteResults();
            _output.WriteLine(run.Table());
            _output.WriteLine("results: " + folder);
            _output.WriteLine("peak working set MiB: " + System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64 / (1024 * 1024));

            Assert.True(run.Total > 0, "no inputs were run");
            var missing = InputPlanner.Kinds.Where(k => !run.Counts.ContainsKey(k)).ToList();
            Assert.True(missing.Count == 0, "kinds without inputs: " + string.Join(", ", missing));

            var report = string.Join("\n", run.Disagreements.GroupBy(d => d.Kind + " | " + d.Reason)
                .OrderByDescending(g => g.Count()).Take(25)
                .Select(g => g.Count() + " x " + g.Key + "  e.g. " + g.First().Id + " " + g.First().Origin));
            Assert.True(run.Disagreements.Count == 0, run.Disagreements.Count + " disagreements, details in " + folder + "\n" + report);
        }
    }
}
