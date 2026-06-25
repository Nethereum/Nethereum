using System;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>Tunable knobs for <see cref="FetchRequestScheduler"/>.</summary>
    /// <param name="PerRequestTimeout">Per-attempt wall-clock timeout. If the
    /// peer doesn't respond in this window the request is reassigned.
    /// Sentinel default = 30s.</param>
    /// <param name="MaxRetriesPerRequest">Hard cap on retries across all
    /// peers before throwing.</param>
    /// <param name="MaxInFlightPerPeer">Cap on concurrent in-flight requests
    /// per peer.</param>
    /// <param name="NoPeerAvailableBackoff">When no peer can be claimed
    /// (all at capacity), how long to wait before re-polling. Sentinel
    /// default = 50ms.</param>
    /// <param name="MaxParallelBodyFetches">Maximum number of peers the
    /// scheduler will fan a single FetchBodiesAsync call out to. The hash
    /// list is split into chunks of <see cref="BodyFetchChunkSize"/> and
    /// dispatched concurrently; chunk count is bounded by both this value
    /// and the number of currently-active peers. Default 4.</param>
    /// <param name="BodyFetchChunkSize">Target chunk size when splitting a
    /// bodies request across multiple peers. Each chunk goes to one peer.
    /// Default 16.</param>
    public sealed record FetchRequestSchedulerOptions(
        TimeSpan PerRequestTimeout = default,
        int MaxRetriesPerRequest = 5,
        int MaxInFlightPerPeer = 1,
        TimeSpan NoPeerAvailableBackoff = default,
        int MaxParallelBodyFetches = 16,
        int BodyFetchChunkSize = 24,
        int MaxParallelReceiptFetches = 16,
        int ReceiptFetchChunkSize = 32)
    {
        public TimeSpan EffectivePerRequestTimeout =>
            PerRequestTimeout == default ? TimeSpan.FromSeconds(30) : PerRequestTimeout;

        public TimeSpan EffectiveNoPeerAvailableBackoff =>
            NoPeerAvailableBackoff == default ? TimeSpan.FromMilliseconds(50) : NoPeerAvailableBackoff;

        public int EffectiveMaxParallelBodyFetches =>
            MaxParallelBodyFetches <= 0 ? 1 : MaxParallelBodyFetches;

        public int EffectiveBodyFetchChunkSize =>
            BodyFetchChunkSize <= 0 ? 24 : BodyFetchChunkSize;

        public int EffectiveMaxParallelReceiptFetches =>
            MaxParallelReceiptFetches <= 0 ? 1 : MaxParallelReceiptFetches;

        public int EffectiveReceiptFetchChunkSize =>
            ReceiptFetchChunkSize <= 0 ? 32 : ReceiptFetchChunkSize;
    }
}
