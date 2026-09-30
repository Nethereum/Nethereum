using System.Collections.Generic;
using Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.IntegrationTests
{
    public class BundlerRpcServerReadmeTraceabilityTests
    {
        private static readonly ReadmeTraceabilityChecker Checker =
            new ReadmeTraceabilityChecker(
                DocSection.AccountAbstraction,
                typeof(BundlerRpcHandlerExtensions).Assembly,
                new[] { "src", "Nethereum.AccountAbstraction.Bundler.RpcServer", "README.md" });

        [Fact]
        public void Given_TheBundlerRpcServerAssembly_Then_AtLeastOneSymbolCarriesTheDocExampleTag()
        {
            if (Checker.TaggedSymbolCount() == 0)
                Assert.Fail("traceability tags missing - no [NethereumDocExample(DocSection.AccountAbstraction, ...)] " +
                            "symbols found in the Nethereum.AccountAbstraction.Bundler.RpcServer assembly");
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
                Assert.Fail(string.Join("\n", failures));
        }
    }
}
