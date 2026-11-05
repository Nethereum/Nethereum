using Nethereum.Documentation;
using System.Numerics;

namespace Nethereum.X402.Blockchain;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "Erc20ApprovalGasSponsoringOptions - facilitator opt-in for sponsored ERC-20 approvals")]
public class Erc20ApprovalGasSponsoringOptions
{
    public bool Enabled { get; set; }

    public BigInteger MaxSponsoredGasCost { get; set; }

    public int MinimumPermitDeadlineSeconds { get; set; } = 120;

    public IErc20ApprovalSponsoredSettlement? Settlement { get; set; }

    public void Validate()
    {
        if (MinimumPermitDeadlineSeconds < 0)
            throw new InvalidOperationException("MinimumPermitDeadlineSeconds must not be negative");

        if (Enabled && MaxSponsoredGasCost <= BigInteger.Zero)
            throw new InvalidOperationException(
                "MaxSponsoredGasCost must be a positive amount in wei when ERC-20 approval gas sponsoring is enabled");
    }
}
