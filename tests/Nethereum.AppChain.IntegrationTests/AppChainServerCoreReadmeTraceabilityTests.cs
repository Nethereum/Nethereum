using System.Collections.Generic;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    public class AppChainServerCoreReadmeTraceabilityTests
    {
        private static readonly ReadmeTraceabilityChecker Checker =
            new ReadmeTraceabilityChecker(
                DocSection.ChainInfrastructure,
                typeof(Nethereum.AppChain.Server.AppChainComposition).Assembly,
                new[] { "src", "Nethereum.AppChain.Server.Core", "README.md" });

        [Fact]
        public void Given_TheAppChainServerCoreAssembly_Then_AtLeastOneSymbolCarriesTheDocExampleTag()
        {
            if (Checker.TaggedSymbolCount() == 0)
                Assert.True(false, "traceability tags missing - no [NethereumDocExample(DocSection.ChainInfrastructure, ...)] " +
                            "symbols found in the AppChainServerCore assembly");
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
