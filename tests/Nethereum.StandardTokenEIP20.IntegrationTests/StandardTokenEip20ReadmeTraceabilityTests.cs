using System.Collections.Generic;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.StandardTokenEIP20.IntegrationTests
{
    public class StandardTokenEip20ReadmeTraceabilityTests
    {
        private static readonly ReadmeTraceabilityChecker Checker =
            new ReadmeTraceabilityChecker(
                DocSection.SmartContracts,
                typeof(Nethereum.StandardTokenEIP20.StandardTokenService).Assembly,
                new[] { "src", "Nethereum.StandardTokenEIP20", "README.md" });

        [Fact]
        public void Given_TheStandardTokenEIP20Assembly_Then_AtLeastOneSymbolCarriesTheDocExampleTag()
        {
            if (Checker.TaggedSymbolCount() == 0)
                Assert.True(false, "traceability tags missing - no [NethereumDocExample(DocSection.SmartContracts, ...)] " +
                            "symbols found in the StandardTokenEIP20 assembly");
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
