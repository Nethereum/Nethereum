using System.Collections.Generic;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.Wallet.UI.Components.Avalonia.UnitTests
{
    public class WalletAvaloniaReadmeTraceabilityTests
    {
        private static readonly ReadmeTraceabilityChecker Checker =
            new ReadmeTraceabilityChecker(
                DocSection.WalletUI,
                typeof(Nethereum.Wallet.UI.Components.Avalonia.Options.AvaloniaWalletComponentOptions).Assembly,
                new[] { "src", "Nethereum.Wallet.UI.Components.Avalonia", "README.md" });

        [Fact]
        public void Given_TheWalletAvaloniaAssembly_Then_AtLeastOneSymbolCarriesTheDocExampleTag()
        {
            if (Checker.TaggedSymbolCount() == 0)
                Assert.True(false, "traceability tags missing - no [NethereumDocExample(DocSection.WalletUI, ...)] " +
                            "symbols found in the WalletAvalonia assembly");
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
