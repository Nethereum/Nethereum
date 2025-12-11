namespace Nethereum.X402.Client;

/// <summary>
/// Exception thrown when a payment amount exceeds the configured maximum.
/// Spec Reference: Section 4.4 - Client Safety and Validation
/// </summary>
public class X402PaymentExceedsMaximumException : Exception
{
    public decimal RequestedAmount { get; }
    public decimal MaximumAllowed { get; }

    public X402PaymentExceedsMaximumException(decimal requestedAmount, decimal maximumAllowed)
        : base($"Payment amount {requestedAmount} USDC exceeds maximum allowed {maximumAllowed} USDC")
    {
        RequestedAmount = requestedAmount;
        MaximumAllowed = maximumAllowed;
    }

    public X402PaymentExceedsMaximumException(decimal requestedAmount, decimal maximumAllowed, string message)
        : base(message)
    {
        RequestedAmount = requestedAmount;
        MaximumAllowed = maximumAllowed;
    }

    public X402PaymentExceedsMaximumException(decimal requestedAmount, decimal maximumAllowed, string message, Exception innerException)
        : base(message, innerException)
    {
        RequestedAmount = requestedAmount;
        MaximumAllowed = maximumAllowed;
    }
}
