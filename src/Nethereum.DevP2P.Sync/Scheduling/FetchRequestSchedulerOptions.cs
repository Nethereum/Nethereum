using System;

namespace Nethereum.DevP2P.Sync.Scheduling
{
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
