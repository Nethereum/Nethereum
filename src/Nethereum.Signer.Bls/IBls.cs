using System;

namespace Nethereum.Signer.Bls
{
    /// <summary>
    /// Defines the operations required from any BLS implementation that Nethereum relies on.
    /// </summary>
    public interface IBls
    {
        /// <summary>
        /// Verifies an aggregate BLS signature (typically sync committees) over one or more
        /// messages and public keys using an ETH2-style domain separation.
        /// </summary>
        /// <param name="aggregateSignature">The aggregated signature bytes.</param>
        /// <param name="publicKeys">The public keys that contributed to the aggregate.</param>
        /// <param name="messages">The message payloads that were signed.</param>
        /// <param name="domain">Domain separation tag (usually forkDigest|domainType).</param>
        /// <returns>True when the aggregate signature is valid.</returns>
        bool VerifyAggregate(byte[] aggregateSignature, byte[][] publicKeys, byte[][] messages, byte[] domain);

        /// <summary>
        /// Aggregates multiple BLS signatures into a single signature.
        /// Used for ERC-4337 BLS signature aggregation to reduce gas costs.
        /// </summary>
        /// <param name="signatures">Array of individual signatures to aggregate.</param>
        /// <returns>The aggregated signature bytes.</returns>
        byte[] AggregateSignatures(byte[][] signatures);

        /// <summary>
        /// Verifies an individual BLS signature over a message.
        /// </summary>
        /// <param name="signature">The signature bytes.</param>
        /// <param name="publicKey">The public key bytes.</param>
        /// <param name="message">The message that was signed.</param>
        /// <returns>True when the signature is valid.</returns>
        bool Verify(byte[] signature, byte[] publicKey, byte[] message);

        /// <summary>
        /// Gets the public key from a signature for validation purposes.
        /// Note: This extracts the public key from the signature format used in ERC-4337,
        /// where the signature includes both the BLS signature and public key.
        /// </summary>
        /// <param name="signatureWithPubKey">The combined signature+publicKey bytes.</param>
        /// <returns>Tuple of (signature, publicKey).</returns>
        (byte[] Signature, byte[] PublicKey) ExtractSignatureAndPublicKey(byte[] signatureWithPubKey);
    }

    public enum BlsImplementationKind
    {
        None,
        HerumiNative,
        Managed
    }

    public interface IBlsEnvironment
    {
        BlsImplementationKind ImplementationKind { get; }
    }
}
