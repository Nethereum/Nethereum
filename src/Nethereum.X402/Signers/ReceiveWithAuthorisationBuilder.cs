using System;
using System.Numerics;
using System.Security.Cryptography;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.X402.Models;

namespace Nethereum.X402.Signers;

/// <summary>
/// Builds Authorization objects for EIP-3009 ReceiveWithAuthorization.
/// Provides TypedData definition and nonce generation.
/// Spec Reference: Section 6.1.2, EIP-3009
///
/// ReceiveWithAuthorization allows the payee (receiver) to submit the authorization
/// and pay for the gas, pulling funds from the payer's account.
/// </summary>
public class ReceiveWithAuthorisationBuilder
{
    /// <summary>
    /// Generates a cryptographically secure random nonce for authorization.
    /// </summary>
    /// <returns>32-byte random nonce</returns>
    public byte[] GenerateNonce()
    {
        var nonce = new byte[32];
        RandomNumberGenerator.Fill(nonce);
        return nonce;
    }

    /// <summary>
    /// Builds an Authorization from PaymentRequirements for ReceiveWithAuthorization pattern.
    /// </summary>
    /// <param name="requirements">Payment requirements from x402 spec</param>
    /// <param name="fromAddress">Payer address (who authorizes the transfer)</param>
    /// <param name="validAfterTimestamp">Optional valid after timestamp (defaults to 10 minutes ago)</param>
    /// <param name="validBeforeTimestamp">Optional valid before timestamp (defaults to 1 hour from now)</param>
    /// <returns>Authorization ready for signing</returns>
    public Authorization BuildFromPaymentRequirements(
        PaymentRequirements requirements,
        string fromAddress,
        BigInteger? validAfterTimestamp = null,
        BigInteger? validBeforeTimestamp = null)
    {
        var validAfter = validAfterTimestamp ?? GetDefaultValidAfter();
        var validBefore = validBeforeTimestamp ?? GetDefaultValidBefore();
        var nonce = GenerateNonce();

        return new Authorization
        {
            From = fromAddress,
            To = requirements.PayTo,
            Value = requirements.MaxAmountRequired,
            ValidAfter = validAfter.ToString(),
            ValidBefore = validBefore.ToString(),
            Nonce = nonce.ToHex(true)
        };
    }

    /// <summary>
    /// Gets the EIP-712 TypedData definition for ReceiveWithAuthorization.
    /// </summary>
    /// <param name="tokenName">Token name (e.g., "USD Coin")</param>
    /// <param name="tokenVersion">Token version (e.g., "2")</param>
    /// <param name="chainId">Chain ID</param>
    /// <param name="verifyingContract">Token contract address</param>
    /// <returns>TypedData definition ready for signing</returns>
    public TypedData<Domain> GetTypedDataForAuthorization(
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
            Types = MemberDescriptionFactory.GetTypesMemberDescription(typeof(Domain), typeof(ReceiveWithAuthorization)),
            PrimaryType = nameof(ReceiveWithAuthorization)
        };
    }

    private BigInteger GetDefaultValidAfter()
    {
        return new BigInteger(DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds());
    }

    private BigInteger GetDefaultValidBefore()
    {
        return new BigInteger(DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds());
    }

    /// <summary>
    /// EIP-3009 ReceiveWithAuthorization message type.
    /// Used for EIP-712 typed data structure.
    /// Note: Has same structure as TransferWithAuthorization but different function selector.
    /// </summary>
    [Struct("ReceiveWithAuthorization")]
    public class ReceiveWithAuthorization
    {
        [Parameter("address", "from", 1)]
        public string From { get; set; } = null!;

        [Parameter("address", "to", 2)]
        public string To { get; set; } = null!;

        [Parameter("uint256", "value", 3)]
        public BigInteger Value { get; set; }

        [Parameter("uint256", "validAfter", 4)]
        public BigInteger ValidAfter { get; set; }

        [Parameter("uint256", "validBefore", 5)]
        public BigInteger ValidBefore { get; set; }

        [Parameter("bytes32", "nonce", 6)]
        public byte[] Nonce { get; set; } = null!;
    }
}
