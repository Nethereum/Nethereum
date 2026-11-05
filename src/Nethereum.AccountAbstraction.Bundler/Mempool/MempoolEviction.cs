using System.Numerics;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Bundler.Mempool
{
    public static class MempoolEviction
    {
        public static MempoolEntry? SelectVictim(IEnumerable<MempoolEntry> pending, MempoolEntry incoming)
        {
            var incomingSender = SenderOf(incoming);
            var incomingFee = PriorityFeeOf(incoming.UserOperation);

            MempoolEntry? victim = null;
            var victimFee = BigInteger.Zero;

            foreach (var group in pending.Where(e => e.State == MempoolEntryState.Pending).GroupBy(SenderOf))
            {
                if (group.Key == incomingSender) continue;

                var tail = group.OrderByDescending(e => e.UserOperation.Nonce).First();
                var fee = PriorityFeeOf(tail.UserOperation);

                if (victim == null || fee < victimFee || (fee == victimFee && tail.SubmittedAt > victim.SubmittedAt))
                {
                    victim = tail;
                    victimFee = fee;
                }
            }

            return victim != null && incomingFee > victimFee ? victim : null;
        }

        private static string SenderOf(MempoolEntry entry) =>
            entry.UserOperation.Sender?.ToLowerInvariant() ?? string.Empty;

        private static BigInteger PriorityFeeOf(PackedUserOperation userOp) =>
            userOp.UnpackGasFees().MaxPriorityFeePerGas;
    }

    public interface IMempoolEvictionLog
    {
        MempoolEntry[] DrainEvicted();
    }

    public interface IMempoolPendingPool
    {
        Task<MempoolEntry[]> GetPendingPoolAsync();
    }

    public interface IMempoolInFlight
    {
        Task<string[]> MarkInFlightAsync(string[] userOpHashes);

        Task ReleaseInFlightAsync(string[] userOpHashes);
    }
}
