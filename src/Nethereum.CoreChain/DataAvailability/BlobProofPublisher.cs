using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Proving;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain.DataAvailability
{
    public class BlobProofPublisher : IProofPublisher
    {
        private readonly IBlobKzgProvider _kzg;
        private readonly IBlobStore _blobStore;

        public BlobProofPublisher(IBlobKzgProvider kzg, IBlobStore blobStore)
        {
            _kzg = kzg ?? throw new ArgumentNullException(nameof(kzg));
            _blobStore = blobStore ?? throw new ArgumentNullException(nameof(blobStore));
        }

        public async Task<ProofPublication> PublishAsync(ProofBundle proof, AnchorScope scope, CancellationToken ct = default)
        {
            if (proof?.ProofBytes == null || proof.ProofBytes.Length == 0)
                return null;

            var payload = BlockProofSubmitter.SerializeProofPayload(new BlockProofResult
            {
                ProofBytes = proof.ProofBytes,
                ElfHash = proof.ElfHash,
                ProverMode = proof.ProverMode,
                BlockNumber = proof.BlockNumber
            });

            var blobs = BlobEncoder.EncodeBlobs(payload);
            var records = new List<BlobSidecarRecord>();
            var commitmentHash = new byte[32];

            using (var sha = SHA256.Create())
            {
                for (int i = 0; i < blobs.Count; i++)
                {
                    var blob = blobs[i];
                    var commitment = _kzg.BlobToKzgCommitment(blob);
                    var kzgProof = _kzg.ComputeBlobKzgProof(blob, commitment);
                    var versionedHash = _kzg.ComputeVersionedHash(commitment);

                    records.Add(new BlobSidecarRecord
                    {
                        Index = i,
                        Blob = blob,
                        KzgCommitment = commitment,
                        KzgProof = kzgProof,
                        VersionedHash = versionedHash
                    });
                }

                var allHashes = new byte[records.Count * 32];
                for (int i = 0; i < records.Count; i++)
                    Array.Copy(records[i].VersionedHash, 0, allHashes, i * 32, 32);
                commitmentHash = sha.ComputeHash(allHashes);
            }

            await _blobStore.StoreBlobsAsync(scope.EndBlock, commitmentHash, records);

            return new ProofPublication
            {
                CommitmentHash = commitmentHash,
                TransactionHash = commitmentHash
            };
        }

        public async Task<byte[]> RetrieveAsync(ProofPublication commitment, CancellationToken ct = default)
        {
            if (commitment?.CommitmentHash == null) return null;

            var blobs = await _blobStore.GetBlobsByTransactionHashAsync(commitment.CommitmentHash);
            if (blobs == null || blobs.Count == 0) return null;

            var blobData = new List<byte[]>();
            foreach (var b in blobs) blobData.Add(b.Blob);
            var decoded = BlobEncoder.DecodeBlobs(blobData);

            return BlockProofSubmitter.DeserializeProofPayload(decoded)?.ProofBytes;
        }
    }
}
