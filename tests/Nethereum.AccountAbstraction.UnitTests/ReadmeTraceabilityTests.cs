using System.Collections.Generic;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests
{
    public class AccountAbstractionReadmeTraceabilityTests
    {
        private static readonly ReadmeTraceabilityChecker Checker =
            new ReadmeTraceabilityChecker(
                DocSection.AccountAbstraction,
                typeof(AATransactionReceipt).Assembly,
                new[] { "src", "Nethereum.AccountAbstraction", "README.md" });

        [Fact]
        public void Given_TheAccountAbstractionAssembly_Then_AtLeastOneSymbolCarriesTheDocExampleTag()
        {
            if (Checker.TaggedSymbolCount() == 0)
                Assert.Fail("traceability tags missing - no [NethereumDocExample(DocSection.AccountAbstraction, ...)] " +
                            "symbols found in the Nethereum.AccountAbstraction assembly");
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

        internal static void AssertNoFailures(IReadOnlyList<string> failures)
        {
            if (failures.Count > 0)
                Assert.Fail(string.Join("\n", failures));
        }
    }

    public class BundlerReadmeTraceabilityTests
    {
        private static readonly ReadmeTraceabilityChecker Checker =
            new ReadmeTraceabilityChecker(
                DocSection.AccountAbstraction,
                typeof(BundlerConfig).Assembly,
                new[] { "src", "Nethereum.AccountAbstraction.Bundler", "README.md" });

        [Fact]
        public void Given_TheBundlerAssembly_Then_AtLeastOneSymbolCarriesTheDocExampleTag()
        {
            if (Checker.TaggedSymbolCount() == 0)
                Assert.Fail("traceability tags missing - no [NethereumDocExample(DocSection.AccountAbstraction, ...)] " +
                            "symbols found in the Nethereum.AccountAbstraction.Bundler assembly");
        }

        [Fact]
        public void Given_ATaggedMethod_Then_TheBundlerReadmeShowsItsNameAndEveryParameter()
        {
            AccountAbstractionReadmeTraceabilityTests.AssertNoFailures(Checker.MethodFailures());
        }

        [Fact]
        public void Given_ATaggedEnum_Then_TheBundlerReadmeShowsEveryMember()
        {
            AccountAbstractionReadmeTraceabilityTests.AssertNoFailures(Checker.EnumFailures());
        }

        [Fact]
        public void Given_ATaggedTypeSurface_Then_TheBundlerReadmeShowsEveryPublicPropertyAndField()
        {
            AccountAbstractionReadmeTraceabilityTests.AssertNoFailures(Checker.TypeSurfaceFailures());
        }
    }
}
