using System.Collections.Generic;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.ENS.UnitTests
{
    public class ReadmeTraceabilityTests
    {
        private static readonly ReadmeTraceabilityChecker Checker =
            new ReadmeTraceabilityChecker(
                DocSection.SmartContracts,
                typeof(Nethereum.ENS.EthTLSService).Assembly,
                new[] { "src", "Nethereum.ENS", "README.md" });

        [Fact]
        public void Given_TheEnsAssembly_Then_AtLeastOneSymbolCarriesTheDocExampleTag()
        {
            if (Checker.TaggedSymbolCount() == 0)
                Assert.True(false, "traceability tags missing - no [NethereumDocExample(DocSection.SmartContracts, ...)] " +
                            "symbols found in the Ens assembly");
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
