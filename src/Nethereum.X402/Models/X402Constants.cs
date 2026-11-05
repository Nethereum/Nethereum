using Nethereum.Documentation;
namespace Nethereum.X402.Models;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "X402Headers - the three x402 HTTP header names")]
public static class X402Headers
{
    public const string PaymentRequired = "PAYMENT-REQUIRED";

    public const string PaymentSignature = "PAYMENT-SIGNATURE";

    public const string PaymentResponse = "PAYMENT-RESPONSE";
}

[NethereumDocExample(DocSection.DeFi, "x402-payments", "X402Schemes - the exact scheme name")]
public static class X402Schemes
{
    public const string Exact = "exact";
}

public static class X402AssetTransferMethods
{
    public const string Eip3009 = "eip3009";

    public const string Permit2 = "permit2";
}

public static class X402PaymentFlows
{
    public const string Authorization = "authorization";

    public const string Upfront = "upfront";
}

[NethereumDocExample(DocSection.DeFi, "x402-payments", "X402Protocol - the protocol version")]
public static class X402Protocol
{
    public const int Version = 2;
}
