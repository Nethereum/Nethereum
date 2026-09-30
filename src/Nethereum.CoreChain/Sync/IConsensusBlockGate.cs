using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain.Sync
{
    public interface IConsensusBlockGate
    {
        Task<ConsensusBlockGateResult> IsBlockCanonicalAsync(
            BlockHeader header,
            byte[] computedBlockHash,
            CancellationToken ct);

        Task OnBlockImportedAsync(BlockHeader header, byte[] blockHash, CancellationToken ct)
            => Task.CompletedTask;
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
