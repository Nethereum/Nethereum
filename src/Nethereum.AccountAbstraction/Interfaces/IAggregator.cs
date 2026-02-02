using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Interfaces
{
    /// <summary>
    /// ERC-4337 IAggregator interface - allows aggregating multiple signatures into one.
    /// This is an advanced feature used to reduce gas costs when multiple user operations
    /// from accounts using the same signature scheme can be validated together.
    ///
    /// Example use cases:
    /// - BLS signature aggregation
    /// - Schnorr signature aggregation
    /// - Multi-sig optimizations
    /// </summary>
    public interface IAggregator
    {
        /// <summary>
        /// Validates the aggregated signature for a batch of user operations.
        /// Reverts if the aggregated signature is invalid.
        /// </summary>
        /// <param name="userOps">Array of user operations to validate</param>
        /// <param name="signature">The aggregated signature</param>
        Task ValidateSignaturesAsync(
            PackedUserOperation[] userOps,
            byte[] signature);

        /// <summary>
        /// Validates that a single user operation's signature can be aggregated.
        /// Returns the authorization data needed for aggregation.
        /// </summary>
        /// <param name="userOp">The user operation to validate</param>
        /// <returns>
        /// Authorization data that will be passed to aggregateSignatures.
        /// The data format is aggregator-specific.
        /// </returns>
        Task<byte[]> ValidateUserOpSignatureAsync(PackedUserOperation userOp);

        /// <summary>
        /// Aggregates multiple signatures into a single aggregated signature.
        /// Called off-chain by the bundler before submitting the bundle.
        /// </summary>
        /// <param name="userOps">Array of user operations whose signatures to aggregate</param>
        /// <returns>The aggregated signature</returns>
        Task<byte[]> AggregateSignaturesAsync(PackedUserOperation[] userOps);
    }
}
