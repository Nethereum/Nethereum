using System;

namespace Nethereum.X402.Client;

public enum PaymentPolicyDimension
{
    Network,
    Asset,
    Recipient
}

public class X402PaymentPolicyViolationException : Exception
{
    public PaymentPolicyDimension Dimension { get; }

    public string RejectedValue { get; }

    public X402PaymentPolicyViolationException(PaymentPolicyDimension dimension, string rejectedValue)
        : base($"Payment refused by client policy: {dimension.ToString().ToLowerInvariant()} '{rejectedValue}' is not in the allow-list.")
    {
        Dimension = dimension;
        RejectedValue = rejectedValue;
    }
}
