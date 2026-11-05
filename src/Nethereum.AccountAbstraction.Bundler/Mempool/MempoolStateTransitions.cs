using System.Numerics;

namespace Nethereum.AccountAbstraction.Bundler.Mempool
{
    public static class MempoolStateTransitions
    {
        public static bool IsOccupying(MempoolEntry entry) =>
            entry.State == MempoolEntryState.Pending || entry.State == MempoolEntryState.Submitted;

        public static bool TrySubmit(MempoolEntry entry, string transactionHash)
        {
            if (!IsOccupying(entry)) return false;
            entry.State = MempoolEntryState.Submitted;
            entry.TransactionHash = transactionHash;
            return true;
        }

        public static bool TryMarkInFlight(MempoolEntry entry)
        {
            if (entry.State != MempoolEntryState.Pending) return false;
            entry.State = MempoolEntryState.Submitted;
            return true;
        }

        public static bool TryRelease(MempoolEntry entry)
        {
            if (entry.State != MempoolEntryState.Submitted || !string.IsNullOrEmpty(entry.TransactionHash)) return false;
            Requeue(entry);
            return true;
        }

        public static bool TryInclude(
            MempoolEntry entry, string transactionHash, BigInteger blockNumber, string? blockHash)
        {
            if (entry.State == MempoolEntryState.Dropped) return false;
            entry.State = MempoolEntryState.Included;
            entry.FinishedAt = DateTimeOffset.UtcNow;
            entry.TransactionHash = transactionHash;
            entry.BlockNumber = blockNumber;
            entry.BlockHash = blockHash;
            entry.Error = null;
            return true;
        }

        public static bool TryFail(MempoolEntry entry, string error)
        {
            if (!IsOccupying(entry)) return false;
            entry.State = MempoolEntryState.Failed;
            entry.FinishedAt = DateTimeOffset.UtcNow;
            entry.Error = error;
            return true;
        }

        public static bool TryRevert(MempoolEntry entry)
        {
            if (entry.State != MempoolEntryState.Submitted) return false;
            Requeue(entry);
            entry.RetryCount++;
            return true;
        }

        public static void Requeue(MempoolEntry entry)
        {
            entry.State = MempoolEntryState.Pending;
            entry.TransactionHash = null;
        }
    }
}
