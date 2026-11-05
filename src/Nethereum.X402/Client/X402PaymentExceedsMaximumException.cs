using Nethereum.Documentation;
namespace Nethereum.X402.Client;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "X402PaymentExceedsMaximumException - the requested amount is above MaxAmount")]
public class X402PaymentExceedsMaximumException : Exception
{
    public string RequestedAmount { get; }

    public string MaximumAllowed { get; }

    public X402PaymentExceedsMaximumException(string requestedAmount, string maximumAllowed)
        : base($"Payment amount {requestedAmount} exceeds the maximum allowed {maximumAllowed} (atomic units)")
    {
        RequestedAmount = requestedAmount;
        MaximumAllowed = maximumAllowed;
    }
}
