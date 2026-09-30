using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Validation
{
    public interface ICanonicalStateRootSource
    {
        string Name { get; }

        Task<(byte[] StateRoot, byte[] BlockHash)> GetCanonicalAsync(
            ulong blockNumber,
            CancellationToken ct);

        Task<CanonicalTip> GetLatestAsync(CancellationToken ct);
    }

    public sealed class CanonicalTip
    {
        public ulong BlockNumber { get; set; }

        public byte[] BlockHash { get; set; } = System.Array.Empty<byte>();

        public byte[] StateRoot { get; set; } = System.Array.Empty<byte>();
    }
}
