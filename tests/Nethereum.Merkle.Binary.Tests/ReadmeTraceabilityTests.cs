using System.Collections.Generic;
using Nethereum.Documentation;
using Nethereum.Merkle.Binary.Nodes;
using Xunit;

namespace Nethereum.Merkle.Binary.Tests
{
    public class ReadmeTraceabilityTests
    {
        private static readonly ReadmeTraceabilityChecker Checker =
            new ReadmeTraceabilityChecker(
                DocSection.ChainInfrastructure,
                typeof(IBinaryNode).Assembly,
                new[] { "src", "Nethereum.Merkle.Binary", "README.md" });

        [Fact]
        public void Given_TheMerkleBinaryAssembly_Then_AtLeastOneSymbolCarriesTheDocExampleTag()
        {
            if (Checker.TaggedSymbolCount() == 0)
                Assert.True(false, "traceability tags missing - no [NethereumDocExample(DocSection.ChainInfrastructure, ...)] " +
                            "symbols found in the Nethereum.Merkle.Binary assembly");
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
