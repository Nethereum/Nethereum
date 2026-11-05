using Nethereum.Documentation;
using System;
using System.Numerics;
using System.Security.Cryptography;
using Nethereum.ABI.EIP712;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.X402.Models;

namespace Nethereum.X402.Signers;

[NethereumDocExample(DocSection.DeFi, "x402-payments", "Eip3009AuthorisationBuilderBase - nonce and authorization building")]
public abstract class Eip3009AuthorisationBuilderBase
{
    public byte[] GenerateNonce()
    {
        var nonce = new byte[32];
        RandomNumberGenerator.Fill(nonce);
        return nonce;
    }

    public Authorization BuildFromPaymentRequirements(
        PaymentRequirements requirements,
        string fromAddress,
        BigInteger? validAfterTimestamp = null,
        BigInteger? validBeforeTimestamp = null)
    {
        var validAfter = validAfterTimestamp ?? GetDefaultValidAfter();
        var validBefore = validBeforeTimestamp ?? GetDefaultValidBefore(requirements);
        var nonce = GenerateNonce();

        return new Authorization
        {
            From = fromAddress,
            To = requirements.PayTo,
            Value = requirements.Amount,
            ValidAfter = validAfter.ToString(),
            ValidBefore = validBefore.ToString(),
            Nonce = nonce.ToHex(true)
        };
    }

    protected TypedData<Domain> BuildTypedData(
        Type messageType,
        string primaryType,
        string tokenName,
        string tokenVersion,
        BigInteger chainId,
        string verifyingContract)
    {
        return new TypedData<Domain>
        {
            Domain = new Domain
            {
                Name = tokenName,
                Version = tokenVersion,
                ChainId = chainId,
                VerifyingContract = verifyingContract
            },
            Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Domain), messageType),
            PrimaryType = primaryType
        };
    }

    private BigInteger GetDefaultValidAfter()
    {
        return new BigInteger(DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds());
    }

    private BigInteger GetDefaultValidBefore(PaymentRequirements requirements)
    {
        var validity = requirements.MaxTimeoutSeconds > 0
            ? TimeSpan.FromSeconds(requirements.MaxTimeoutSeconds)
            : TimeSpan.FromHours(1);
        return new BigInteger(DateTimeOffset.UtcNow.Add(validity).ToUnixTimeSeconds());
    }
}
