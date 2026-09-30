using System;
using System.Security.Cryptography;
using Nethereum.Model;

namespace Nethereum.DevP2P.Sync.Mempool
{
    public static class BlobSidecarValidator
    {
        public const byte VersionedHashKzgVersion = 0x01;

        public static bool HasSidecar(Transaction4844 tx)
        {
            var sidecar = tx?.Sidecar;
            return sidecar?.Blobs != null && sidecar.Blobs.Count > 0;
        }

        public static bool HasValidVersionedHashes(Transaction4844 tx)
        {
            var sidecar = tx?.Sidecar;
            if (sidecar?.Commitments == null || sidecar.Blobs == null || sidecar.Proofs == null) return false;
            if (tx.BlobVersionedHashes == null) return false;

            var count = tx.BlobVersionedHashes.Count;
            if (sidecar.Commitments.Count != count) return false;
            if (sidecar.Blobs.Count != count) return false;
            if (sidecar.Proofs.Count != count) return false;

            using var sha = SHA256.Create();
            for (int i = 0; i < count; i++)
            {
                var declared = tx.BlobVersionedHashes[i];
                if (declared == null || declared.Length < 1 || declared[0] != VersionedHashKzgVersion) return false;

                var commitment = sidecar.Commitments[i];
                if (commitment == null) return false;

                var digest = sha.ComputeHash(commitment);
                digest[0] = VersionedHashKzgVersion;
                if (!digest.AsSpan().SequenceEqual(declared)) return false;
            }
            return true;
        }
    }
}
