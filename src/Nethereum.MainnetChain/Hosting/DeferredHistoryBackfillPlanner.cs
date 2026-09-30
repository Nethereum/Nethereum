namespace Nethereum.MainnetChain.Hosting
{
    public enum DeferredHistoryBackfillDecision
    {
        NotDeferred,
        PeeringUnavailable,
        HeaderSkeletonUnavailable,
        NothingToFill,
        Run,
    }

    public static class DeferredHistoryBackfillPlanner
    {
        public static DeferredHistoryBackfillDecision Decide(
            bool runAfterStateSync, bool peeringAvailable, bool headerSkeletonAvailable, ulong pivot, ulong cursor)
        {
            if (!runAfterStateSync) return DeferredHistoryBackfillDecision.NotDeferred;
            if (!peeringAvailable) return DeferredHistoryBackfillDecision.PeeringUnavailable;
            if (!headerSkeletonAvailable) return DeferredHistoryBackfillDecision.HeaderSkeletonUnavailable;
            if (pivot == 0 || cursor >= pivot) return DeferredHistoryBackfillDecision.NothingToFill;
            return DeferredHistoryBackfillDecision.Run;
        }
    }
}
