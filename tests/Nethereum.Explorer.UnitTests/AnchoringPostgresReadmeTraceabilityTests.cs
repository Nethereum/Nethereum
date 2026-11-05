using System.Collections.Generic;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.Explorer.UnitTests
{
    public class AnchoringPostgresReadmeTraceabilityTests
    {
        private static readonly ReadmeTraceabilityChecker Checker =
            new ReadmeTraceabilityChecker(
                DocSection.DataIndexing,
                typeof(Nethereum.AppChain.Anchoring.Postgres.AnchorIndexProcessingOptions).Assembly,
                new[] { "src", "Nethereum.AppChain.Anchoring.Postgres", "README.md" });

        [Fact]
        public void Given_TheAnchoringPostgresAssembly_Then_AtLeastOneSymbolCarriesTheDocExampleTag()
        {
            if (Checker.TaggedSymbolCount() == 0)
                Assert.True(false, "traceability tags missing - no [NethereumDocExample(DocSection.DataIndexing, ...)] " +
                            "symbols found in the AnchoringPostgres assembly");
        }

        [Fact]
        public void Given_ATaggedMethod_Then_TheReadmeShowsItsNameAndEveryParameter()
        {
            AssertNoFailures(Checker.MethodFailures());
        }

        [Fact]
        public void Given_ATaggedEnum_Then_TheReadmeShowsEveryMember()
        {
            AssertNoFailures(Checker.EnumFailures());
        }

        [Fact]
        public void Given_ATaggedTypeSurface_Then_TheReadmeShowsEveryPublicPropertyAndField()
        {
            AssertNoFailures(Checker.TypeSurfaceFailures());
        }

        private static void AssertNoFailures(IReadOnlyList<string> failures)
        {
            if (failures.Count > 0)
                Assert.True(false, string.Join("\n", failures));
        }
    }
}
