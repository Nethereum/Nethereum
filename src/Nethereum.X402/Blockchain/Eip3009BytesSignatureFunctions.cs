using Nethereum.Documentation;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using System.Numerics;

namespace Nethereum.X402.Blockchain;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "TransferWithAuthorizationBytesSignatureFunction - transferWithAuthorization with a bytes signature")]
[Function("transferWithAuthorization")]
public class TransferWithAuthorizationBytesSignatureFunction : FunctionMessage
{
    [Parameter("address", "from", 1)]
    public virtual string AuthorisationFrom { get; set; }

    [Parameter("address", "to", 2)]
    public virtual string AuthorisationTo { get; set; }

    [Parameter("uint256", "value", 3)]
    public virtual BigInteger Value { get; set; }

    [Parameter("uint256", "validAfter", 4)]
    public virtual BigInteger ValidAfter { get; set; }

    [Parameter("uint256", "validBefore", 5)]
    public virtual BigInteger ValidBefore { get; set; }

    [Parameter("bytes32", "nonce", 6)]
    public virtual byte[] AuthorisationNonce { get; set; }

    [Parameter("bytes", "signature", 7)]
    public virtual byte[] Signature { get; set; }
}

[NethereumDocExample(DocSection.DeFi, "x402-payments", "ReceiveWithAuthorizationBytesSignatureFunction - receiveWithAuthorization with a bytes signature")]
[Function("receiveWithAuthorization")]
public class ReceiveWithAuthorizationBytesSignatureFunction : FunctionMessage
{
    [Parameter("address", "from", 1)]
    public virtual string From { get; set; }

    [Parameter("address", "to", 2)]
    public virtual string To { get; set; }

    [Parameter("uint256", "value", 3)]
    public virtual BigInteger Value { get; set; }

    [Parameter("uint256", "validAfter", 4)]
    public virtual BigInteger ValidAfter { get; set; }

    [Parameter("uint256", "validBefore", 5)]
    public virtual BigInteger ValidBefore { get; set; }

    [Parameter("bytes32", "nonce", 6)]
    public virtual byte[] AuthorisationNonce { get; set; }

    [Parameter("bytes", "signature", 7)]
    public virtual byte[] Signature { get; set; }
}
