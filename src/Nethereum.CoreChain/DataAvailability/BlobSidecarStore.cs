using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain.DataAvailability
{
    internal static class BlobSidecarStore
    {
        internal static async Task<byte[]> StoreAsync(
            IBlobKzgProvider kzg, IBlobStore blobStore, byte[] payload, BigInteger blockNumber)
        {
            var blobs = BlobEncoder.EncodeBlobs(payload);
            var records = new List<BlobSidecarRecord>();

            for (int i = 0; i < blobs.Count; i++)
            {
                var blob = blobs[i];
                var commitment = kzg.BlobToKzgCommitment(blob);
                var kzgProof = kzg.ComputeBlobKzgProof(blob, commitment);
                var versionedHash = kzg.ComputeVersionedHash(commitment);

                records.Add(new BlobSidecarRecord
                {
                    Index = i,
                    Blob = blob,
                    KzgCommitment = commitment,
                    KzgProof = kzgProof,
                    VersionedHash = versionedHash
                });
            }

            var commitmentHash = HashOfVersionedHashes(records);
            await blobStore.StoreBlobsAsync(blockNumber, commitmentHash, records);
            return commitmentHash;
        }

        internal static async Task<byte[]> LoadAsync(IBlobStore blobStore, byte[] commitmentHash)
        {
            var blobs = await blobStore.GetBlobsByTransactionHashAsync(commitmentHash);
            if (blobs == null || blobs.Count == 0) return null;

            var blobData = new List<byte[]>();
            foreach (var b in blobs) blobData.Add(b.Blob);
            return BlobEncoder.DecodeBlobs(blobData);
        }

        private static byte[] HashOfVersionedHashes(List<BlobSidecarRecord> records)
        {
            var allHashes = new byte[records.Count * 32];
            for (int i = 0; i < records.Count; i++)
                Array.Copy(records[i].VersionedHash, 0, allHashes, i * 32, 32);

            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(allHashes);
            }
        }
    }
}
