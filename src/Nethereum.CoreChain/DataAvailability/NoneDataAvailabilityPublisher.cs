using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.DataAvailability
{
    public class NoneDataAvailabilityPublisher : IDataAvailabilityPublisher
    {
        public Task<DaPublication> PublishAsync(DaPayload payload, AnchorScope scope, CancellationToken ct = default)
        {
            return Task.FromResult(new DaPublication { Commitment = null });
        }

        public Task<byte[]> RetrieveAsync(DaCommitment commitment, CancellationToken ct = default)
        {
            return Task.FromResult(System.Array.Empty<byte>());
        }
    }
}
