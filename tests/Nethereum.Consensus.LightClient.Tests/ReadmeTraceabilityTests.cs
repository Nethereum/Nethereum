using System.Collections.Generic;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.Consensus.LightClient.Tests
{
    public class ReadmeTraceabilityTests
    {
        private static readonly ReadmeTraceabilityChecker Checker =
            new ReadmeTraceabilityChecker(
                DocSection.Consensus,
                typeof(LightClientService).Assembly,
                new[] { "src", "Nethereum.Consensus.LightClient", "README.md" });

        [Fact]
        public void Given_TheLightClientAssembly_Then_AtLeastOneSymbolCarriesTheDocExampleTag()
        {
            Assert.NotEqual(0, Checker.TaggedSymbolCount());
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
            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }
    }
}
