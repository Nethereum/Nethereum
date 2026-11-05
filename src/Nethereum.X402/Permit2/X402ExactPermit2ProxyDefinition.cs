using Nethereum.Documentation;
using System.Numerics;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.Contracts.Standards.Permit2;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;

namespace Nethereum.X402.Permit2;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "SettleFunction - x402ExactPermit2Proxy settle")]
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

[NethereumDocExample(DocSection.DeFi, "x402-payments", "Eip2612PermitParams - the EIP-2612 permit struct of settleWithPermit")]
[Struct("EIP2612Permit")]
public class Eip2612PermitParams
{
    [Parameter("uint256", "value", 1)]
    public virtual BigInteger Value { get; set; }

    [Parameter("uint256", "deadline", 2)]
    public virtual BigInteger Deadline { get; set; }

    [Parameter("bytes32", "r", 3)]
    public virtual byte[] R { get; set; } = null!;

    [Parameter("bytes32", "s", 4)]
    public virtual byte[] S { get; set; } = null!;

    [Parameter("uint8", "v", 5)]
    public virtual byte V { get; set; }
}

[NethereumDocExample(DocSection.DeFi, "x402-payments", "SettleWithPermitFunction - x402ExactPermit2Proxy settleWithPermit")]
[Function("settleWithPermit")]
public class SettleWithPermitFunction : FunctionMessage
{
    [Parameter("tuple", "permit2612", 1, "EIP2612Permit")]
    public virtual Eip2612PermitParams Permit2612 { get; set; } = null!;

    [Parameter("tuple", "permit", 2, "PermitTransferFrom")]
    public virtual PermitTransferFrom Permit { get; set; } = null!;

    [Parameter("address", "owner", 3)]
    public virtual string Owner { get; set; } = null!;

    [Parameter("tuple", "witness", 4, "Witness")]
    public virtual Witness Witness { get; set; } = null!;

    [Parameter("bytes", "signature", 5)]
    public virtual byte[] Signature { get; set; } = null!;
}

[NethereumDocExample(DocSection.DeFi, "x402-payments", "Permit2612AmountMismatchError - proxy custom error")]
[Error("Permit2612AmountMismatch")]
public class Permit2612AmountMismatchError : IErrorDTO
{
}

[NethereumDocExample(DocSection.DeFi, "x402-payments", "InvalidDestinationError - proxy custom error")]
[Error("InvalidDestination")]
public class InvalidDestinationError : IErrorDTO
{
}

[NethereumDocExample(DocSection.DeFi, "x402-payments", "InvalidOwnerError - proxy custom error")]
[Error("InvalidOwner")]
public class InvalidOwnerError : IErrorDTO
{
}

[NethereumDocExample(DocSection.DeFi, "x402-payments", "PaymentTooEarlyError - proxy custom error")]
[Error("PaymentTooEarly")]
public class PaymentTooEarlyError : IErrorDTO
{
}

[NethereumDocExample(DocSection.DeFi, "x402-payments", "Eip2612NoncesFunction - token nonces call")]
[Function("nonces", "uint256")]
public class Eip2612NoncesFunction : FunctionMessage
{
    [Parameter("address", "owner", 1)]
    public virtual string Owner { get; set; } = null!;
}
