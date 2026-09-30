using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.DataAvailability
{
    public class CalldataDataAvailabilityPublisher : IDataAvailabilityPublisher
    {
        private readonly CompressionAlgo _compression;

        public CalldataDataAvailabilityPublisher(CompressionAlgo compression = CompressionAlgo.Brotli)
        {
            _compression = compression;
        }

        public Task<DaPublication> PublishAsync(DaPayload payload, AnchorScope scope, CancellationToken ct = default)
        {
            if (payload?.Data == null || payload.Data.Length == 0)
                return Task.FromResult(new DaPublication { Commitment = null });

            var envelope = CompressedEnvelope.Wrap(payload.Data, _compression);

            byte[] commitmentHash;
            using (var sha = SHA256.Create())
            {
                commitmentHash = sha.ComputeHash(envelope);
            }

            return Task.FromResult(new DaPublication
            {
                Commitment = new DaCommitment
                {
                    Type = DaMode.Calldata,
                    CommitmentHash = commitmentHash,
                    Length = payload.Data.Length
                },
                CompressedPayload = envelope
            });
        }

        public Task<byte[]> RetrieveAsync(DaCommitment commitment, CancellationToken ct = default)
        {
            return Task.FromResult(Array.Empty<byte>());
        }
    }
}
