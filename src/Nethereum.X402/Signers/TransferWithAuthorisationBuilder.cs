using System.Numerics;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.X402.Signers;

/// <summary>
/// Builds Authorization objects for EIP-3009 TransferWithAuthorization.
/// Provides TypedData definition and nonce generation.
/// Spec Reference: Section 6.1.2, EIP-3009
/// </summary>
public class TransferWithAuthorisationBuilder : Eip3009AuthorisationBuilderBase
{
    /// <summary>
    /// Gets the EIP-712 TypedData definition for TransferWithAuthorization.
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
        => BuildTypedData(
            typeof(TransferWithAuthorization),
            nameof(TransferWithAuthorization),
            tokenName,
            tokenVersion,
            chainId,
            verifyingContract);

    /// <summary>
    /// EIP-3009 TransferWithAuthorization message type.
    /// Used for EIP-712 typed data structure.
    /// </summary>
    [Struct("TransferWithAuthorization")]
    public class TransferWithAuthorization
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
