using Nethereum.X402.IntegrationTests.Helpers;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Integration;

[Collection("X402 Reference Forward Interop")]
[Trait("Category", "LiveNetwork")]
public class X402ReferencePythonInteropTests
{
    [SkippableTheory]
    [InlineData(InteropCase.Eip3009)]
    [InlineData(InteropCase.Permit2Eip2612)]
    [InlineData(InteropCase.Upfront)]
    public Task PythonReferenceClient_IsPaidThroughOurServerAndFacilitator(InteropCase interopCase)
    {
        var reason = X402ReferenceTools.PythonSkipReason();
        Skip.If(reason != null, reason);
        return InteropScenario.ReferenceClientPaysAsync(interopCase, X402ReferenceTools.RunPythonClient);
    }
}
