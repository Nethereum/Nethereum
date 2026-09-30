using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class HistoryDebtBackpressureTests
    {
        private const long PauseAt = 100;
        private const long ResumeAt = 70;

        [Theory]
        [InlineData(0, false, false)]
        [InlineData(99, false, false)]
        [InlineData(101, false, true)]
        [InlineData(99, true, true)]
        [InlineData(71, true, true)]
        [InlineData(70, true, false)]
        [InlineData(0, true, false)]
        public void EvaluateHysteresisPause_HysteresisTransitions(long worst, bool paused, bool expected)
            => Assert.Equal(expected,
                Stores.RocksDbWritePressureMonitor.EvaluateHysteresisPause(worst, paused, PauseAt, ResumeAt));
    }
}
