using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class SnapshotNotTakenGuardTests
    {
        private static ExecutionStateService NewState() =>
            new ExecutionStateService(new MockNodeDataService());

        [Fact]
        public void Given_TheNotTakenSentinel_When_Compared_Then_ItCanNeverBeARealSnapshotId()
        {
            var first = NewState().TakeSnapshot();

            Assert.Equal(0, first);
            Assert.True(TransactionExecutionContext.NotTaken < 0);
            Assert.NotEqual(first, TransactionExecutionContext.NotTaken);
        }

        [Fact]
        public void Given_AContextWhoseSnapshotWasNeverTaken_When_Reverted_Then_ItThrowsRatherThanUnwindingToTheFirst()
        {
            var state = NewState();
            state.TakeSnapshot();
            var ctx = new TransactionExecutionContext();

            Assert.Throws<System.InvalidOperationException>(
                () => state.RevertToSnapshot(ctx.PrepPhaseSnapshotId));
        }

        [Fact]
        public void Given_AContextWhoseSnapshotWasNeverTaken_When_Committed_Then_ItAlsoThrows()
        {
            var state = NewState();
            state.TakeSnapshot();
            var ctx = new TransactionExecutionContext();

            Assert.Throws<System.InvalidOperationException>(
                () => state.CommitSnapshot(ctx.TransactionSnapshotId));
        }

        [Fact]
        public void Given_ASnapshotThatWasTaken_When_Reverted_Then_ItStillWorks()
        {
            var state = NewState();
            var id = state.TakeSnapshot();

            state.RevertToSnapshot(id);
        }
    }
}
