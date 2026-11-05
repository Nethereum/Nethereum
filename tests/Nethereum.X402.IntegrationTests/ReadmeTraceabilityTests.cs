using Nethereum.Documentation;
using Nethereum.X402.Models;
using Xunit;

namespace Nethereum.X402.IntegrationTests;

public class X402ReadmeTraceabilityTests
{
    private static readonly ReadmeTraceabilityChecker Checker =
        new(
            DocSection.DeFi,
            typeof(PaymentRequirements).Assembly,
            new[] { "src", "Nethereum.X402", "README.md" });

    [Fact]
    public void Given_TheX402Assembly_Then_AtLeastOneSymbolCarriesTheDocExampleTag()
    {
        if (Checker.TaggedSymbolCount() == 0)
            Assert.Fail("traceability tags missing - no [NethereumDocExample(DocSection.DeFi, ...)] " +
                        "symbols found in the Nethereum.X402 assembly");
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
