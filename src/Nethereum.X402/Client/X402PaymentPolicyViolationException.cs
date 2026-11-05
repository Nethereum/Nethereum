using Nethereum.Documentation;
using System;

namespace Nethereum.X402.Client;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "PaymentPolicyDimension - which allow-list refused a payment")]
public enum PaymentPolicyDimension
{
    Network,
    Asset,
    Recipient
}

[NethereumDocExample(DocSection.DeFi, "x402-payments", "X402PaymentPolicyViolationException - a payment refused by the client policy")]
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
