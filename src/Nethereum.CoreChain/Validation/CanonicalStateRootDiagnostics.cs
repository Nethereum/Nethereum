using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Validation
{
    public static class CanonicalStateRootDiagnostics
    {
        public static async Task<DivergenceVerdict> DiagnoseAsync(
            this ICanonicalStateRootSource source,
            ulong blockNumber,
            byte[] peerHeaderStateRoot,
            byte[] ourComputedStateRoot,
            CancellationToken ct)
        {
            var (canonicalRoot, canonicalHash) = await source.GetCanonicalAsync(blockNumber, ct)
                .ConfigureAwait(false);
            if (canonicalRoot == null)
            {
                return new DivergenceVerdict(
                    Outcome: DivergenceOutcome.SourceUnavailable,
                    CanonicalStateRoot: null,
                    CanonicalBlockHash: null,
                    SourceName: source.Name,
                    Detail: $"Source has no canonical answer at block {blockNumber:N0}.");
            }
            bool peerAgreesCanonical = SequenceEquals(peerHeaderStateRoot, canonicalRoot);
            bool oursAgreesCanonical = SequenceEquals(ourComputedStateRoot, canonicalRoot);

            if (oursAgreesCanonical)
            {
                return new DivergenceVerdict(
                    Outcome: DivergenceOutcome.PeerLied,
                    CanonicalStateRoot: canonicalRoot,
                    CanonicalBlockHash: canonicalHash,
                    SourceName: source.Name,
                    Detail: "Our state root matches canonical; peer header was wrong-fork.");
            }
            if (peerAgreesCanonical)
            {
                return new DivergenceVerdict(
                    Outcome: DivergenceOutcome.EvmBug,
                    CanonicalStateRoot: canonicalRoot,
                    CanonicalBlockHash: canonicalHash,
                    SourceName: source.Name,
                    Detail: "Peer header matches canonical; our re-execution diverged. EVM bug.");
            }
            return new DivergenceVerdict(
                Outcome: DivergenceOutcome.PeerLied,
                CanonicalStateRoot: canonicalRoot,
                CanonicalBlockHash: canonicalHash,
                SourceName: source.Name,
                Detail: "Neither our root nor the peer's matches canonical; peer header was wrong-fork.");
        }

        private static bool SequenceEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
