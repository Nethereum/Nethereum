using Nethereum.CoreChain.RocksDB;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class ResumeBodyCursorReconcileTests
    {
        [Fact]
        public void Given_TheFreezerLeadsTheCursor_When_Reconciled_Then_CursorAdvancesToTheLastFrozenBlock()
        {
            Assert.Equal(99UL, RocksDbChainStoreBundle.ReconcileBodyCursorToFreezerHead(bodyCursor: 79, freezerItems: 100, freezeBoundary: 1000));
        }

        [Fact]
        public void Given_TheObservedWedgeGap_When_Reconciled_Then_CursorClearsTheAlreadyFrozenGap()
        {
            Assert.Equal(15615939UL,
                RocksDbChainStoreBundle.ReconcileBodyCursorToFreezerHead(bodyCursor: 15615819, freezerItems: 15615940, freezeBoundary: 16000000));
        }

        [Fact]
        public void Given_TheCursorLeadsAboveTheFreezeBoundary_When_Reconciled_Then_ItIsKept()
        {
            Assert.Equal(500UL, RocksDbChainStoreBundle.ReconcileBodyCursorToFreezerHead(bodyCursor: 500, freezerItems: 100, freezeBoundary: 200));
        }

        [Fact]
        public void Given_ACrashLostTheUncommittedFreezeTail_When_CursorLeadsWithinTheFrozenBand_Then_ItIsPulledDownToTheCommittedHead()
        {
            Assert.Equal(90UL, RocksDbChainStoreBundle.ReconcileBodyCursorToFreezerHead(bodyCursor: 95, freezerItems: 91, freezeBoundary: 100));
        }

        [Fact]
        public void Given_ACrashLostTheFreezeTail_ButCursorSitsAboveTheBoundary_When_Reconciled_Then_ItIsKept()
        {
            Assert.Equal(200UL, RocksDbChainStoreBundle.ReconcileBodyCursorToFreezerHead(bodyCursor: 200, freezerItems: 91, freezeBoundary: 100));
        }

        [Fact]
        public void Given_TheCursorEqualsTheLastFrozenBlock_When_Reconciled_Then_ItIsUnchanged()
        {
            Assert.Equal(79UL, RocksDbChainStoreBundle.ReconcileBodyCursorToFreezerHead(bodyCursor: 79, freezerItems: 80, freezeBoundary: 1000));
        }

        [Fact]
        public void Given_TheFreezerIsOneBlockAhead_When_Reconciled_Then_CursorAdvancesByExactlyThatBlock()
        {
            Assert.Equal(80UL, RocksDbChainStoreBundle.ReconcileBodyCursorToFreezerHead(bodyCursor: 79, freezerItems: 81, freezeBoundary: 1000));
        }

        [Fact]
        public void Given_AnEmptyFreezerButTheCursorLeadsWithinTheFrozenBand_When_Reconciled_Then_ItResetsToZero()
        {
            Assert.Equal(0UL, RocksDbChainStoreBundle.ReconcileBodyCursorToFreezerHead(bodyCursor: 79, freezerItems: 0, freezeBoundary: 1000));
        }

        [Fact]
        public void Given_AnEmptyFreezerAndTheCursorSitsAboveTheBoundary_When_Reconciled_Then_ItIsKept_NoUnderflow()
        {
            Assert.Equal(79UL, RocksDbChainStoreBundle.ReconcileBodyCursorToFreezerHead(bodyCursor: 79, freezerItems: 0, freezeBoundary: 50));
        }

        [Fact]
        public void Given_AnEmptyFreezerAndFreshCursorAtZero_When_Reconciled_Then_ItStaysZero()
        {
            Assert.Equal(0UL, RocksDbChainStoreBundle.ReconcileBodyCursorToFreezerHead(bodyCursor: 0, freezerItems: 0, freezeBoundary: 1000));
        }

        [Fact]
        public void Given_AFreshCursorAtZero_When_TheFreezerLeads_Then_ItAdvancesToTheLastFrozenBlock()
        {
            Assert.Equal(99UL, RocksDbChainStoreBundle.ReconcileBodyCursorToFreezerHead(bodyCursor: 0, freezerItems: 100, freezeBoundary: 1000));
        }

        [Fact]
        public void Given_ANegativeFreezeBoundary_When_CursorLeads_Then_ItIsKept_NoUnderflow()
        {
            Assert.Equal(50UL, RocksDbChainStoreBundle.ReconcileBodyCursorToFreezerHead(bodyCursor: 50, freezerItems: 10, freezeBoundary: -90000));
        }
    }
}
