using Nethereum.X402.IntegrationTests.Helpers;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Integration;

[Collection("X402 Reference Forward Interop")]
[Trait("Category", "LiveNetwork")]
public class X402ReferenceGoInteropTests
{
    private static void SkipWithoutGo()
    {
        var reason = X402ReferenceTools.GoSkipReason();
        Skip.If(reason != null, reason);
    }

    [SkippableTheory]
    [InlineData(InteropCase.Eip3009)]
    [InlineData(InteropCase.Permit2Eip2612)]
    [InlineData(InteropCase.Upfront)]
    public Task GoReferenceClient_IsPaidThroughOurServerAndFacilitator(InteropCase interopCase)
    {
        SkipWithoutGo();
        return InteropScenario.ReferenceClientPaysAsync(interopCase, X402ReferenceTools.RunGoClient);
    }

    [SkippableTheory]
    [InlineData(InteropCase.Eip3009)]
    [InlineData(InteropCase.Permit2Eip2612)]
    public Task OurClient_IsSettledByGoReferenceFacilitator(InteropCase interopCase)
    {
        SkipWithoutGo();
        return InteropScenario.NethereumClientPaysAsync(interopCase, X402ReferenceTools.StartGoFacilitator);
    }
}
