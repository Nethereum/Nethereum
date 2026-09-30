namespace Nethereum.X402.Models;

public static class X402Headers
{
    public const string PaymentRequired = "PAYMENT-REQUIRED";

    public const string PaymentSignature = "PAYMENT-SIGNATURE";

    public const string PaymentResponse = "PAYMENT-RESPONSE";
}

public static class X402Schemes
{
    public const string Exact = "exact";
}

public static class X402AssetTransferMethods
{
    /// <summary>EIP-3009 transferWithAuthorization / receiveWithAuthorization (default).</summary>
    public const string Eip3009 = "eip3009";

    public const string Permit2 = "permit2";
}

public static class X402Protocol
{
    public const int Version = 2;
}
