using System;
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

            var commitmentHash = await BlobSidecarStore.StoreAsync(_kzg, _blobStore, payload, scope.EndBlock);

            return new ProofPublication
            {
                CommitmentHash = commitmentHash,
                TransactionHash = commitmentHash
            };
        }

        public async Task<byte[]> RetrieveAsync(ProofPublication commitment, CancellationToken ct = default)
        {
            if (commitment?.CommitmentHash == null) return null;

            var decoded = await BlobSidecarStore.LoadAsync(_blobStore, commitment.CommitmentHash);
            if (decoded == null) return null;

            return BlockProofSubmitter.DeserializeProofPayload(decoded)?.ProofBytes;
        }
    }
}
