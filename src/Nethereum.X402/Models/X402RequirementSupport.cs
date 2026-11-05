using Nethereum.Documentation;
namespace Nethereum.X402.Models;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "X402RequirementSupport - which requirements this library supports")]
public static class X402RequirementSupport
{
    public static bool IsAuthorizationFlow(PaymentRequirements requirements)
    {
        if (!ExactSchemeExtra.TryRead(requirements, out var extra))
            return false;

        var paymentFlow = extra?.PaymentFlow;
        return paymentFlow == null || paymentFlow == X402PaymentFlows.Authorization;
    }

    public static bool IsUpfrontFlow(PaymentRequirements requirements) =>
        ExactSchemeExtra.TryRead(requirements, out var extra) && extra?.PaymentFlow == X402PaymentFlows.Upfront;

    public static string ResolveAssetTransferMethod(PaymentRequirements requirements) =>
        ExactSchemeExtra.TryRead(requirements, out var extra)
            ? extra?.AssetTransferMethod ?? X402AssetTransferMethods.Eip3009
            : string.Empty;

    public static bool IsSupportedByThisLibrary(PaymentRequirements requirements)
    {
        var assetTransferMethod = ResolveAssetTransferMethod(requirements);
        return requirements?.Scheme == X402Schemes.Exact
            && (IsAuthorizationFlow(requirements) || IsUpfrontFlow(requirements))
            && (assetTransferMethod == X402AssetTransferMethods.Eip3009
                || assetTransferMethod == X402AssetTransferMethods.Permit2);
    }
}
