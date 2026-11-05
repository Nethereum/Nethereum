namespace Nethereum.AccountAbstraction.Bundler.Mempool
{
    public static class MempoolPruneRule
    {
        public static bool IsExpired(
            MempoolEntry entry, DateTimeOffset now, TimeSpan entryTtl, TimeSpan finishedRetention)
        {
            if (entry.State == MempoolEntryState.Included || entry.State == MempoolEntryState.Failed)
            {
                return now - (entry.FinishedAt ?? entry.SubmittedAt) > finishedRetention;
            }

            if (now - entry.SubmittedAt > entryTtl)
            {
                return true;
            }

            return entry.ValidUntil.HasValue && entry.ValidUntil.Value < (ulong)now.ToUnixTimeSeconds();
        }
    }
}
