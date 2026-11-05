using System;
using System.Collections.Generic;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class SignerReadmeTraceabilityTests
    {
        private static readonly ReadmeTraceabilityChecker Checker =
            new ReadmeTraceabilityChecker(
                DocSection.Signing,
                typeof(Nethereum.Signer.Secp256k1Contexts).Assembly,
                new[] { "src", "Nethereum.Signer", "README.md" });

        [Fact]
        public void Given_TheSignerAssembly_Then_AtLeastOneSymbolCarriesTheDocExampleTag()
        {
            if (Checker.TaggedSymbolCount() == 0)
                Assert.True(false, "traceability tags missing - no [NethereumDocExample(DocSection.Signing, ...)] " +
                            "symbols found in the Nethereum.Signer assembly");
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
