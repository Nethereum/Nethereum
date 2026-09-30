using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Util;
using Nethereum.X402.Models;

namespace Nethereum.X402.Client;

public class PaymentPolicy
{
    /// <summary>CAIP-2 networks the client may pay on (e.g. "eip155:8453"). Empty = any network.</summary>
    public List<string> AllowedNetworks { get; } = new();

    public List<string> AllowedAssets { get; } = new();

    public List<string> AllowedRecipients { get; } = new();

    public void Assert(PaymentRequirements requirement)
    {
        if (requirement == null) throw new ArgumentNullException(nameof(requirement));

        if (AllowedNetworks.Count > 0 &&
            !AllowedNetworks.Any(n => string.Equals(n, requirement.Network, StringComparison.OrdinalIgnoreCase)))
        {
            throw new X402PaymentPolicyViolationException(PaymentPolicyDimension.Network, requirement.Network);
        }

        if (AllowedAssets.Count > 0 &&
            !AllowedAssets.Any(a => IsSameAddress(a, requirement.Asset)))
        {
            throw new X402PaymentPolicyViolationException(PaymentPolicyDimension.Asset, requirement.Asset);
        }

        if (AllowedRecipients.Count > 0 &&
            !AllowedRecipients.Any(r => IsSameAddress(r, requirement.PayTo)))
        {
            throw new X402PaymentPolicyViolationException(PaymentPolicyDimension.Recipient, requirement.PayTo);
        }
    }

    private static bool IsSameAddress(string allowed, string candidate) =>
        !string.IsNullOrEmpty(candidate) && !string.IsNullOrEmpty(allowed) && allowed.IsTheSameAddress(candidate);
}
