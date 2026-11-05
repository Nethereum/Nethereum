using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain.DataAvailability
{
    public class BlobDataAvailabilityPublisher : IDataAvailabilityPublisher
    {
        private readonly IBlobKzgProvider _kzg;
        private readonly IBlobStore _blobStore;

        public BlobDataAvailabilityPublisher(IBlobKzgProvider kzg, IBlobStore blobStore)
        {
            _kzg = kzg ?? throw new ArgumentNullException(nameof(kzg));
            _blobStore = blobStore ?? throw new ArgumentNullException(nameof(blobStore));
        }

        public async Task<DaPublication> PublishAsync(DaPayload payload, AnchorScope scope, CancellationToken ct = default)
        {
            if (payload?.Data == null || payload.Data.Length == 0)
                return new DaPublication { Commitment = null };

            var commitmentHash = await BlobSidecarStore.StoreAsync(_kzg, _blobStore, payload.Data, scope.EndBlock);

            return new DaPublication
            {
                Commitment = new DaCommitment
                {
                    Type = DaMode.Public,
                    CommitmentHash = commitmentHash,
                    TransactionHash = commitmentHash,
                    Length = payload.Data.Length
                }
            };
        }

        public async Task<byte[]> RetrieveAsync(DaCommitment commitment, CancellationToken ct = default)
        {
            if (commitment?.CommitmentHash == null) return null;

            return await BlobSidecarStore.LoadAsync(_blobStore, commitment.CommitmentHash);
        }
    }
}
