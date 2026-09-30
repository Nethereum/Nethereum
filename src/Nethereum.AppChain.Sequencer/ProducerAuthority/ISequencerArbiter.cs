using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.AppChain.Sequencer.ProducerAuthority
{
    public interface ISequencerArbiter
    {
        Task<LeaseGrant?> TryAcquireOrRenewAsync(string candidateNodeId, CancellationToken ct = default);

        Task ReleaseAsync(string nodeId, long fencingToken, CancellationToken ct = default);

        Task<SequencerStatus> CurrentAsync(CancellationToken ct = default);
    }

    public sealed record LeaseGrant(string NodeId, long FencingToken, DateTimeOffset ExpiresAtUtc);

    public sealed record SequencerStatus(string? NodeId, long FencingToken, DateTimeOffset? ExpiresAtUtc);
}
