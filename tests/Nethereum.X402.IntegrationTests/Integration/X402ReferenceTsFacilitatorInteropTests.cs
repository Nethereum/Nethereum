using Nethereum.X402.IntegrationTests.Helpers;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Integration;

[Collection("X402 Reference Forward Interop")]
[Trait("Category", "LiveNetwork")]
public class X402ReferenceTsFacilitatorInteropTests
{
    [SkippableTheory]
    [InlineData(InteropCase.Eip3009)]
    [InlineData(InteropCase.Permit2)]
    [InlineData(InteropCase.Permit2Eip2612)]
    [InlineData(InteropCase.Permit2Erc20)]
    public Task OurClient_IsSettledByTsReferenceFacilitator(InteropCase interopCase)
    {
        var reason = X402ReferenceTools.NodeSkipReason("reference-facilitator.mjs");
        Skip.If(reason != null, reason);
        return InteropScenario.NethereumClientPaysAsync(interopCase, X402ReferenceTools.StartTsFacilitator);
    }
}
