using System.Collections.Generic;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class ReadmeTraceabilityTests
    {
        private static readonly ReadmeTraceabilityChecker Checker =
            new ReadmeTraceabilityChecker(
                DocSection.ChainInfrastructure,
                typeof(BlockValidityCheck).Assembly,
                new[] { "src", "Nethereum.CoreChain", "README.md" });

        [Fact]
        public void Given_TheCoreChainAssembly_Then_AtLeastOneSymbolCarriesTheDocExampleTag()
        {
            var tagged = Checker.TaggedSymbolCount();
            if (tagged == 0)
                Assert.True(false, "traceability tags missing - no [NethereumDocExample(DocSection.ChainInfrastructure, ...)] " +
                            "symbols found in the Nethereum.CoreChain assembly");
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
