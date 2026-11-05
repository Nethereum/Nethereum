using Nethereum.Documentation;
using System.Numerics;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.Contracts.Standards.Permit2;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.X402.Models;

namespace Nethereum.X402.Permit2;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "Permit2AuthorizationMapper - map a Permit2 authorization to contract messages")]
public static class Permit2AuthorizationMapper
{
    public static PermitWitnessTransferFrom ToWitnessMessage(this Permit2Authorization auth) => new()
    {
        Permitted = new TokenPermissions { Token = auth.Permitted.Token, Amount = X402Number.Parse(auth.Permitted.Amount) },
        Spender = auth.Spender,
        Nonce = X402Number.Parse(auth.Nonce),
        Deadline = X402Number.Parse(auth.Deadline),
        Witness = new Witness { To = auth.Witness.To, ValidAfter = X402Number.Parse(auth.Witness.ValidAfter) }
    };

    public static SettleFunction ToSettleFunction(this Permit2Authorization auth, string signatureHex) => new()
    {
        Permit = new PermitTransferFrom
        {
            Permitted = new TokenPermissions { Token = auth.Permitted.Token, Amount = X402Number.Parse(auth.Permitted.Amount) },
            Nonce = X402Number.Parse(auth.Nonce),
            Deadline = X402Number.Parse(auth.Deadline)
        },
        Owner = auth.From,
        Witness = new Witness { To = auth.Witness.To, ValidAfter = X402Number.Parse(auth.Witness.ValidAfter) },
        Signature = signatureHex.HexToByteArray()
    };

    public static SettleWithPermitFunction ToSettleWithPermitFunction(
        this Permit2Authorization auth, string signatureHex, Eip2612PermitParams permit2612)
    {
        var settle = auth.ToSettleFunction(signatureHex);
        return new SettleWithPermitFunction
        {
            Permit2612 = permit2612,
            Permit = settle.Permit,
            Owner = settle.Owner,
            Witness = settle.Witness,
            Signature = settle.Signature
        };
    }
}
