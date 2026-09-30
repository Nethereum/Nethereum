using System;
using System.Collections.Generic;
using Nethereum.Model;

namespace Nethereum.EVM.Precompiles.Kzg
{
    public class BlobSidecarBuilder
    {
        private readonly IKzgOperations _kzg;

        public BlobSidecarBuilder(IKzgOperations kzg)
        {
            _kzg = kzg ?? throw new ArgumentNullException(nameof(kzg));
        }

        public (BlobSidecar Sidecar, List<byte[]> VersionedHashes) BuildFromData(byte[] data)
        {
            var blobs = BlobEncoder.EncodeBlobs(data);
            return BuildFromBlobs(blobs);
        }

        public (BlobSidecar Sidecar, List<byte[]> VersionedHashes) BuildFromBlobs(List<byte[]> blobs)
        {
            if (blobs == null || blobs.Count == 0)
                throw new ArgumentException("At least one blob is required", nameof(blobs));
            if (blobs.Count > BlobEncoder.MAX_BLOBS_PER_BLOCK)
                throw new ArgumentException(
                    $"Too many blobs: {blobs.Count}, max is {BlobEncoder.MAX_BLOBS_PER_BLOCK}", nameof(blobs));

            var commitments = new List<byte[]>(blobs.Count);
            var proofs = new List<byte[]>(blobs.Count);
            var versionedHashes = new List<byte[]>(blobs.Count);

            foreach (var blob in blobs)
            {
                if (blob.Length != BlobEncoder.BLOB_SIZE)
                    throw new ArgumentException($"Blob must be {BlobEncoder.BLOB_SIZE} bytes, got {blob.Length}");

                var commitment = _kzg.BlobToKzgCommitment(blob);
                var proof = _kzg.ComputeBlobKzgProof(blob, commitment);
                var versionedHash = _kzg.ComputeVersionedHash(commitment);

                commitments.Add(commitment);
                proofs.Add(proof);
                versionedHashes.Add(versionedHash);
            }

            var sidecar = new BlobSidecar(blobs, commitments, proofs);
            return (sidecar, versionedHashes);
        }
    }
}
