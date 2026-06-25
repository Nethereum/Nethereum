using System;
using System.Security.Cryptography;
using Nethereum.Model;

namespace Nethereum.DevP2P.IntegrationTests.Helpers
{
    /// <summary>
    /// EIP-4844 versioned-hash verification — confirms each
    /// <c>blob_versioned_hash</c> in a <see cref="Transaction4844"/> matches the
    /// <c>VERSIONED_HASH_VERSION_KZG ‖ sha256(commitment)[1:]</c> derivation from
    /// the corresponding KZG commitment in its sidecar.
    /// <para>
    /// Used by the eth-test conformance harness's BlobTxWithMismatchedSidecar
    /// sub-test, which expects us to disconnect peers that advertise blob txs
    /// whose commitments don't reproduce the declared versioned hashes.
    /// </para>
    /// </summary>
    public static class BlobSidecarValidator
    {
        /// <summary>
        /// First byte of an EIP-4844 versioned blob hash — flags it as a KZG-versioned
        /// commitment digest. Defined in EIP-4844 as <c>VERSIONED_HASH_VERSION_KZG</c>.
        /// </summary>
        public const byte VersionedHashKzgVersion = 0x01;

        /// <summary>
        /// Returns true when every <see cref="Transaction4844.BlobVersionedHashes"/>
        /// entry matches the derivation
        /// <c>0x01 ‖ sha256(<see cref="BlobSidecar.Commitments"/>[i])[1:]</c>,
        /// and the sidecar is present and length-aligned with the declared hashes.
        /// </summary>
        public static bool HasValidVersionedHashes(Transaction4844 tx)
        {
            if (tx?.Sidecar?.Commitments == null) return false;
            if (tx.Sidecar.Commitments.Count != tx.BlobVersionedHashes.Count) return false;

            using var sha = SHA256.Create();
            for (int i = 0; i < tx.BlobVersionedHashes.Count; i++)
            {
                var digest = sha.ComputeHash(tx.Sidecar.Commitments[i]);
                digest[0] = VersionedHashKzgVersion;
                if (!digest.AsSpan().SequenceEqual(tx.BlobVersionedHashes[i])) return false;
            }
            return true;
        }
    }
}
