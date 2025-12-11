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
