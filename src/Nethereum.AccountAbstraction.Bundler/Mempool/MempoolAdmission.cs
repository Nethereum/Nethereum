namespace Nethereum.AccountAbstraction.Bundler.Mempool
{
    public sealed class MempoolAdmissionDecision
    {
        public MempoolAddOutcome Outcome { get; init; }
        public MempoolEntry? Replaced { get; init; }
        public MempoolEntry? Evicted { get; init; }
        public bool Accepted => Outcome == MempoolAddOutcome.Added || Outcome == MempoolAddOutcome.Replaced;
    }

    public static class MempoolAdmission
    {
        public static MempoolAdmissionDecision Decide(
            bool hashExists,
            MempoolEntry? occupant,
            int pendingCount,
            int capacity,
            Func<IEnumerable<MempoolEntry>> pendingPool,
            MempoolEntry incoming)
        {
            if (hashExists) return Reject(MempoolAddOutcome.RejectedDuplicate);

            if (occupant != null && MempoolStateTransitions.IsOccupying(occupant))
            {
                if (occupant.State != MempoolEntryState.Pending) return Reject(MempoolAddOutcome.RejectedDuplicate);
                if (!MempoolReplacementRules.IsValidFeeBump(occupant.UserOperation, incoming.UserOperation))
                    return Reject(MempoolAddOutcome.RejectedUnderpriced);
                return new MempoolAdmissionDecision { Outcome = MempoolAddOutcome.Replaced, Replaced = occupant };
            }

            if (pendingCount < capacity) return new MempoolAdmissionDecision { Outcome = MempoolAddOutcome.Added };

            var victim = MempoolEviction.SelectVictim(pendingPool(), incoming);
            return victim == null
                ? Reject(MempoolAddOutcome.RejectedFull)
                : new MempoolAdmissionDecision { Outcome = MempoolAddOutcome.Added, Evicted = victim };
        }

        private static MempoolAdmissionDecision Reject(MempoolAddOutcome outcome) =>
            new MempoolAdmissionDecision { Outcome = outcome };
    }
}
