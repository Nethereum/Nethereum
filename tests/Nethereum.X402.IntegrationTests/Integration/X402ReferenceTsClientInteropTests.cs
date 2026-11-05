using Nethereum.X402.IntegrationTests.Helpers;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Integration;

[Collection("X402 Reference Forward Interop")]
[Trait("Category", "LiveNetwork")]
public class X402ReferenceTsClientInteropTests
{
    private static Task PayAsync(string version, InteropCase interopCase)
    {
        var reason = X402ReferenceTools.NodeSkipReason("reference-client.mjs");
        Skip.If(reason != null, reason);
        return InteropScenario.ReferenceClientPaysAsync(
            interopCase, (rpc, key, url) => X402ReferenceTools.RunTsClient(version, rpc, key, url));
    }

    [SkippableTheory]
    [InlineData("2.28.0")]
    [InlineData("2.10.0")]
    public Task TsReferenceClient_Eip3009_IsPaidThroughOurServerAndFacilitator(string version) =>
        PayAsync(version, InteropCase.Eip3009);

    [SkippableTheory]
    [InlineData("2.28.0")]
    [InlineData("2.10.0")]
    public Task TsReferenceClient_Permit2_IsPaidThroughOurServerAndFacilitator(string version) =>
        PayAsync(version, InteropCase.Permit2);

    [SkippableTheory]
    [InlineData("2.28.0")]
    [InlineData("2.10.0")]
    public Task TsReferenceClient_Permit2WithEip2612Sponsoring_IsPaidThroughOurServerAndFacilitator(string version) =>
        PayAsync(version, InteropCase.Permit2Eip2612);

    [SkippableTheory]
    [InlineData("2.28.0")]
    [InlineData("2.10.0")]
    public Task TsReferenceClient_Permit2WithErc20ApprovalSponsoring_IsPaidThroughOurServerAndFacilitator(string version) =>
        PayAsync(version, InteropCase.Permit2Erc20);

    [SkippableTheory]
    [InlineData("2.28.0")]
    [InlineData("2.10.0")]
    public Task TsReferenceClient_UpfrontRoute_IsPaidThroughOurServerAndFacilitator(string version) =>
        PayAsync(version, InteropCase.Upfront);
}
