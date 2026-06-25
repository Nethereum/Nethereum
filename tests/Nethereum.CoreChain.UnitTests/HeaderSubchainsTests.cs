using System.Collections.Generic;
using Nethereum.CoreChain.Storage;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class HeaderSubchainsTests
    {
        private static HeaderSyncState State(params (ulong head, ulong tail, ulong next)[] segs)
        {
            var list = new List<HeaderSubchain>();
            foreach (var (head, tail, next) in segs)
                list.Add(new HeaderSubchain { Head = head, Tail = tail, Next = next });
            return new HeaderSyncState { SchemaVersion = HeaderSyncStateRlpEncoder.CurrentSchemaVersion, Subchains = list };
        }

        [Fact]
        public void OpenTip_OnEmpty_OpensSingleSegmentAtTip()
        {
            var s = HeaderSubchains.OpenTip(HeaderSyncState.Empty, 100);
            Assert.Single(s.Subchains);
            Assert.Equal(100UL, s.Subchains[0].Head);
            Assert.Equal(100UL, s.Subchains[0].Tail);
            Assert.Equal(99UL, s.Subchains[0].Next);
            Assert.Equal(100UL, HeaderSubchains.TrustedTip(s));
        }

        [Fact]
        public void OpenTip_AboveCurrentTop_PrependsDisjointSegment()
        {
            var s = HeaderSubchains.OpenTip(State((100, 50, 49)), 200);
            Assert.Equal(2, s.Subchains.Count);
            Assert.Equal(200UL, s.Subchains[0].Head);
            Assert.Equal(200UL, s.Subchains[0].Tail);
            Assert.Equal(100UL, s.Subchains[1].Head);
            Assert.Equal(200UL, HeaderSubchains.TrustedTip(s));
        }

        [Fact]
        public void OpenTip_AtOrBelowTop_IsNoOp()
        {
            var start = State((100, 50, 49));
            Assert.Same(start, HeaderSubchains.OpenTip(start, 100));
            Assert.Same(start, HeaderSubchains.OpenTip(start, 80));
        }

        [Fact]
        public void RecordDescent_LowersTailAndNext()
        {
            var s = HeaderSubchains.RecordDescent(State((100, 100, 99)), segmentHead: 100, newTail: 40);
            Assert.Single(s.Subchains);
            Assert.Equal(100UL, s.Subchains[0].Head);
            Assert.Equal(40UL, s.Subchains[0].Tail);
            Assert.Equal(39UL, s.Subchains[0].Next);
        }

        [Fact]
        public void RecordDescent_ToGenesis_SetsNextZero()
        {
            var s = HeaderSubchains.RecordDescent(State((100, 100, 99)), 100, 0);
            Assert.Equal(0UL, s.Subchains[0].Tail);
            Assert.Equal(0UL, s.Subchains[0].Next);
        }

        [Fact]
        public void RecordDescent_NoDownwardProgress_IsNoOp()
        {
            var start = State((100, 50, 49));
            Assert.Same(start, HeaderSubchains.RecordDescent(start, 100, 50));
            Assert.Same(start, HeaderSubchains.RecordDescent(start, 100, 60));
        }

        [Fact]
        public void RecordDescent_UnknownHead_IsNoOp()
        {
            var start = State((100, 50, 49));
            Assert.Same(start, HeaderSubchains.RecordDescent(start, segmentHead: 999, newTail: 10));
        }

        [Fact]
        public void RecordDescent_LinkingTwoSegments_MergesIntoOne()
        {
            // [50..100] below, [200..200] above (a freshly opened tip). Walking the
            // upper segment down to 101 makes it touch the lower segment's Head+1 → merge.
            var disjoint = State((200, 200, 199), (100, 50, 49));
            var merged = HeaderSubchains.RecordDescent(disjoint, segmentHead: 200, newTail: 101);

            Assert.Single(merged.Subchains);
            Assert.Equal(200UL, merged.Subchains[0].Head);
            Assert.Equal(50UL, merged.Subchains[0].Tail);
            Assert.Equal(49UL, merged.Subchains[0].Next); // continues descending from the lower edge
        }

        [Fact]
        public void RecordDescent_StillShortOfLink_StaysDisjoint()
        {
            var disjoint = State((200, 200, 199), (100, 50, 49));
            var still = HeaderSubchains.RecordDescent(disjoint, segmentHead: 200, newTail: 150);
            Assert.Equal(2, still.Subchains.Count);
            Assert.Equal(150UL, still.Subchains[0].Tail);
        }

        [Fact]
        public void FullFlow_TipToZero_ThenTipAdvance_FillsGapAndMerges()
        {
            // Initial skeleton: tip 1000 walked all the way to genesis.
            var s = HeaderSubchains.OpenTip(HeaderSyncState.Empty, 1000);
            s = HeaderSubchains.RecordDescent(s, 1000, 0);
            Assert.Single(s.Subchains);
            Assert.Equal(0UL, s.Subchains[0].Tail);

            // Tip advances to 1100 — a new disjoint segment opens.
            s = HeaderSubchains.OpenTip(s, 1100);
            Assert.Equal(2, s.Subchains.Count);
            Assert.Equal(1100UL, HeaderSubchains.TrustedTip(s));

            // Fill the bounded gap [1001..1100] by walking the new segment to 1001 → merges to [0..1100].
            s = HeaderSubchains.RecordDescent(s, 1100, 1001);
            Assert.Single(s.Subchains);
            Assert.Equal(1100UL, s.Subchains[0].Head);
            Assert.Equal(0UL, s.Subchains[0].Tail);
        }
    }
}
