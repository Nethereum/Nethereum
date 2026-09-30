namespace Nethereum.X402.Client;

/// <summary>
/// Exception thrown when a payment amount (in atomic units) exceeds the configured maximum.
/// Spec Reference: Section 4.4 - Client Safety and Validation
/// </summary>
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
