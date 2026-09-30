using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.DataAvailability
{
    public class AnchorPayloadSection
    {
        public AnchorPayloadSectionType Type { get; init; }
        public byte[] Bytes { get; init; }
    }

    public interface IAnchorPayloadContributor
    {
        AnchorPayloadSectionType Kind { get; }
        Task<AnchorPayloadSection> ContributeAsync(AnchorScope scope, CancellationToken ct = default);
    }
}
