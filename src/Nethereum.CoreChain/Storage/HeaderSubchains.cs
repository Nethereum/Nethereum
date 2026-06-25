using System.Collections.Generic;

namespace Nethereum.CoreChain.Storage
{
    /// <summary>
    /// Pure, side-effect-free operations over <see cref="HeaderSyncState"/>'s segment list: open a new
    /// segment when the trusted tip advances, record how far a backward walk descended, and merge
    /// segments once their ranges link up. Kept immutable-in / immutable-out so the merge invariants
    /// are unit-testable without a store or a live walker — the orchestrator persists the result via
    /// <see cref="IChainMetadataStore.SaveHeaderSyncState"/>.
    /// <para>
    /// Invariant on the returned state: segments are ordered by <see cref="HeaderSubchain.Head"/>
    /// descending, are non-overlapping, and each <c>[Tail..Head]</c> is one contiguous,
    /// parent-hash-validated run. <see cref="HeaderSubchain.Next"/> is the next block to fetch walking
    /// down (Tail - 1, or 0 once the run has reached genesis).
    /// </para>
    /// </summary>
    public static class HeaderSubchains
    {
        /// <summary>The latest trusted tip the skeleton is anchored to — the top segment's
        /// <see cref="HeaderSubchain.Head"/>, or 0 when nothing has been laid yet. This is what
        /// <c>eth_syncing</c> reports as <c>highestBlock</c>.</summary>
        public static ulong TrustedTip(HeaderSyncState state)
            => state.Subchains.Count > 0 ? state.Subchains[0].Head : 0;

        /// <summary>
        /// Register an advanced trusted tip. When <paramref name="tipBlock"/> rises above the current
        /// top segment, a fresh segment <c>[tip..tip]</c> opens; walking it down fills the bounded gap
        /// to the old top and then merges. A tip at or below the current top is already covered, so this
        /// is a no-op (returns the same state).
        /// </summary>
        public static HeaderSyncState OpenTip(HeaderSyncState state, ulong tipBlock)
        {
            var subs = state.Subchains;
            if (subs.Count > 0 && tipBlock <= subs[0].Head)
                return state;

            var opened = new List<HeaderSubchain>(subs.Count + 1)
            {
                new HeaderSubchain { Head = tipBlock, Tail = tipBlock, Next = tipBlock == 0 ? 0 : tipBlock - 1 },
            };
            opened.AddRange(subs);
            return Normalise(opened);
        }

        /// <summary>
        /// Record that the walk anchored at <paramref name="segmentHead"/> has validated headers down
        /// to <paramref name="newTail"/> (inclusive). Lowers that segment's Tail/Next and merges any
        /// segments that now touch. A head that matches no segment (already merged) or a Tail that does
        /// not advance downward leaves the state unchanged.
        /// </summary>
        public static HeaderSyncState RecordDescent(HeaderSyncState state, ulong segmentHead, ulong newTail)
        {
            var subs = new List<HeaderSubchain>(state.Subchains);
            int idx = subs.FindIndex(s => s.Head == segmentHead);
            if (idx < 0) return state;
            if (newTail >= subs[idx].Tail) return state;

            subs[idx] = subs[idx] with { Tail = newTail, Next = newTail == 0 ? 0 : newTail - 1 };
            return Normalise(subs);
        }

        // Order by Head descending, then fold adjacent segments whose ranges have become contiguous.
        // Two segments link when the higher one's Tail has descended to touch (or pass) the lower one's
        // Head + 1; the merged run keeps the higher Head and the lower Tail/Next (the descent continues
        // from the lower edge).
        private static HeaderSyncState Normalise(List<HeaderSubchain> subs)
        {
            subs.Sort((a, b) => b.Head.CompareTo(a.Head));

            var merged = new List<HeaderSubchain>(subs.Count);
            foreach (var s in subs)
            {
                if (merged.Count > 0)
                {
                    var top = merged[merged.Count - 1];
                    if (top.Tail <= s.Head + 1)
                    {
                        merged[merged.Count - 1] = new HeaderSubchain
                        {
                            Head = top.Head,
                            Tail = s.Tail < top.Tail ? s.Tail : top.Tail,
                            Next = s.Next,
                        };
                        continue;
                    }
                }
                merged.Add(s);
            }

            return new HeaderSyncState
            {
                SchemaVersion = HeaderSyncStateRlpEncoder.CurrentSchemaVersion,
                Subchains = merged,
            };
        }
    }
}
