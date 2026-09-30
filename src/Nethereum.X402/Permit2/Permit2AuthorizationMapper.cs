using System.Numerics;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.Contracts.Standards.Permit2;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.X402.Models;

namespace Nethereum.X402.Permit2;

public static class Permit2AuthorizationMapper
{
    /// <summary>The EIP-712 message the payer signs (includes the spender; see the signer).</summary>
    public static PermitWitnessTransferFrom ToWitnessMessage(this Permit2Authorization auth) => new()
    {
        Permitted = new TokenPermissions { Token = auth.Permitted.Token, Amount = BigInteger.Parse(auth.Permitted.Amount) },
        Spender = auth.Spender,
        Nonce = BigInteger.Parse(auth.Nonce),
        Deadline = BigInteger.Parse(auth.Deadline),
        Witness = new Witness { To = auth.Witness.To, ValidAfter = BigInteger.Parse(auth.Witness.ValidAfter) }
    };

    /// <summary>
    /// The x402ExactPermit2Proxy.settle arguments: the on-chain permit tuple (no spender), the owner,
    /// the witness, and the signature. <paramref name="signatureHex"/> is the payer's EIP-712 signature.
    /// </summary>
    public static SettleFunction ToSettleFunction(this Permit2Authorization auth, string signatureHex) => new()
    {
        Permit = new PermitTransferFrom
        {
            Permitted = new TokenPermissions { Token = auth.Permitted.Token, Amount = BigInteger.Parse(auth.Permitted.Amount) },
            Nonce = BigInteger.Parse(auth.Nonce),
            Deadline = BigInteger.Parse(auth.Deadline)
        },
        Owner = auth.From,
        Witness = new Witness { To = auth.Witness.To, ValidAfter = BigInteger.Parse(auth.Witness.ValidAfter) },
        Signature = signatureHex.HexToByteArray()
    };
}
