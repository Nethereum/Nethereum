using Nethereum.Documentation;
using System.Numerics;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.Constants;
using Nethereum.Contracts.Standards.Permit2;

namespace Nethereum.X402.Permit2;

public static class X402Permit2Addresses
{
    public const string Permit2 = CommonAddresses.PERMIT2_ADDRESS;

    public const string ExactPermit2Proxy = "0x402085c248EeA27D92E8b30b2C58ed07f9E20001";
}

[NethereumDocExample(DocSection.DeFi, "x402-payments", "Witness - the Permit2 witness struct")]
[Struct("Witness")]
public class Witness
{
    [Parameter("address", "to", 1)]
    public virtual string To { get; set; } = null!;

    [Parameter("uint256", "validAfter", 2)]
    public virtual BigInteger ValidAfter { get; set; }
}

[NethereumDocExample(DocSection.DeFi, "x402-payments", "PermitWitnessTransferFrom - the Permit2 witness transfer message")]
[Struct("PermitWitnessTransferFrom")]
public class PermitWitnessTransferFrom
{
    [Parameter("tuple", "permitted", 1, "TokenPermissions")]
    public virtual TokenPermissions Permitted { get; set; } = null!;

    [Parameter("address", "spender", 2)]
    public virtual string Spender { get; set; } = null!;

    [Parameter("uint256", "nonce", 3)]
    public virtual BigInteger Nonce { get; set; }

    [Parameter("uint256", "deadline", 4)]
    public virtual BigInteger Deadline { get; set; }

    [Parameter("tuple", "witness", 5, "Witness")]
    public virtual Witness Witness { get; set; } = null!;
}
