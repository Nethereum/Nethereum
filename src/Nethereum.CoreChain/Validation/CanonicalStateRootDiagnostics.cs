using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Validation
{
    /// <summary>
    /// Pure-function diagnosis of a state-root mismatch given a canonical
    /// source. Extracted from any specific source so AppChain followers,
    /// SyncNode auto-rewind, and audit-replay tools all classify
    /// divergences with identical logic.
    /// </summary>
    public static class CanonicalStateRootDiagnostics
    {
        /// <summary>
        /// Compare the peer's claimed state root and our own re-execution
        /// result against the canonical answer from
        /// <paramref name="source"/>. The verdict tells the caller what to
        /// do: rewind + ban peer (PeerLied), halt with EVM bug (EvmBug),
        /// or try another source (SourceUnavailable).
        /// </summary>
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
            // Neither side matches — peer was on a wrong fork AND our
            // execution against that wrong-fork header produced something
            // else too. This is the bad-peer case (an EVM bug would still
            // surface against a correct peer header at some block; if both
            // we and the peer disagree with canonical, the peer is
            // almost certainly lying).
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
