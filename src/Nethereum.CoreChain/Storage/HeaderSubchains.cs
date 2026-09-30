using System.Collections.Generic;

namespace Nethereum.CoreChain.Storage
{
    public static class HeaderSubchains
    {
        public static ulong TrustedTip(HeaderSyncState state)
            => state.Subchains.Count > 0 ? state.Subchains[0].Head : 0;

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

        public static HeaderSyncState RecordDescent(HeaderSyncState state, ulong segmentHead, ulong newTail)
        {
            var subs = new List<HeaderSubchain>(state.Subchains);
            int idx = subs.FindIndex(s => s.Head == segmentHead);
            if (idx < 0) return state;
            if (newTail >= subs[idx].Tail) return state;

            subs[idx] = subs[idx] with { Tail = newTail, Next = newTail == 0 ? 0 : newTail - 1 };
            return Normalise(subs);
        }

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
