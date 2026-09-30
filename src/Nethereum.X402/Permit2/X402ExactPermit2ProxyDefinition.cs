using System.Numerics;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.Contracts.Standards.Permit2;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.X402.Permit2;

[Function("settle")]
public class SettleFunction : FunctionMessage
{
    [Parameter("tuple", "permit", 1, "PermitTransferFrom")]
    public virtual PermitTransferFrom Permit { get; set; } = null!;

    [Parameter("address", "owner", 2)]
    public virtual string Owner { get; set; } = null!;

    [Parameter("tuple", "witness", 3, "Witness")]
    public virtual Witness Witness { get; set; } = null!;

    [Parameter("bytes", "signature", 4)]
    public virtual byte[] Signature { get; set; } = null!;
}
