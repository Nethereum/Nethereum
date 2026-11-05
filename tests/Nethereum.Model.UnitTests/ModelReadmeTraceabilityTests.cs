using System;
using System.Collections.Generic;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.Model.UnitTests
{
    public class ModelReadmeTraceabilityTests
    {
        private static readonly ReadmeTraceabilityChecker Checker =
            new ReadmeTraceabilityChecker(
                DocSection.CoreFoundation,
                typeof(Nethereum.Model.TransactionFactory).Assembly,
                new[] { "src", "Nethereum.Model", "README.md" });

        [Fact]
        public void Given_TheModelAssembly_Then_AtLeastOneSymbolCarriesTheDocExampleTag()
        {
            if (Checker.TaggedSymbolCount() == 0)
                Assert.True(false, "traceability tags missing - no [NethereumDocExample(DocSection.CoreFoundation, ...)] " +
                            "symbols found in the Nethereum.Model assembly");
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
                Assert.True(false, string.Join(Environment.NewLine, failures));
        }
    }
}
