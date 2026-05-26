using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.DataAvailability
{
    public class ContentKey
    {
        public long ChainId { get; init; }
        public long StartBlock { get; init; }
        public long EndBlock { get; init; }
    }

    public class DaPayload
    {
        public byte[] Data { get; init; }
        public ContentKey Key { get; init; }
        public DaPayloadKind Kind { get; init; }
    }

    public class DaCommitment
    {
        public DaMode Type { get; init; }
        public byte[] CommitmentHash { get; init; }
        public byte[] TransactionHash { get; init; }
        public int Offset { get; init; }
        public int Length { get; init; }
    }

    public class DaPublication
    {
        public DaCommitment Commitment { get; init; }
        public byte[] CompressedPayload { get; init; }
    }

    public interface IDataAvailabilityPublisher
    {
        Task<DaPublication> PublishAsync(DaPayload payload, AnchorScope scope, CancellationToken ct = default);
        Task<byte[]> RetrieveAsync(DaCommitment commitment, CancellationToken ct = default);
    }
}
