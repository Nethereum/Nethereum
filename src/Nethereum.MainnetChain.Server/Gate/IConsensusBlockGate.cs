using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.MainnetChain.Server.Gate
{
    /// <summary>
    /// Pluggable consensus admission check for an execution-layer block being imported by the follower.
    /// Implementations decide whether a block hash is consistent with the consensus view the host trusts
    /// (e.g. a beacon-chain finalized header carried over by a light client). The follower's
    /// <see cref="Nethereum.CoreChain.Sync.IBlockExecutor"/> consults this gate before applying the
    /// block; a returned <c>false</c> aborts import and surfaces a divergence verdict.
    /// </summary>
    public interface IConsensusBlockGate
    {
        Task<ConsensusBlockGateResult> IsBlockCanonicalAsync(
            BlockHeader header,
            byte[] computedBlockHash,
            CancellationToken ct);
    }

    public readonly struct ConsensusBlockGateResult
    {
        public ConsensusBlockGateResult(bool accepted, string? reason)
        {
            Accepted = accepted;
            Reason = reason;
        }

        public bool Accepted { get; }
        public string? Reason { get; }

        public static ConsensusBlockGateResult Accept() => new ConsensusBlockGateResult(true, null);
        public static ConsensusBlockGateResult Reject(string reason) => new ConsensusBlockGateResult(false, reason);
    }
}
