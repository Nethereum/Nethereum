using Nethereum.AppChain.Anchoring.Metrics;
using Nethereum.AppChain.Sequencer.Metrics;
using Nethereum.AppChain.Sync.Metrics;
using Nethereum.CoreChain.Metrics;
using Xunit;

namespace Nethereum.AppChain.Metrics.UnitTests
{
    public class BlockProductionMetricsTests
    {
        private const string ChainId = "420420";

        [Fact]
        public void RecordBlockProduced_UpdatesAllValues()
        {
            using var metrics = new BlockProductionMetrics(ChainId);

            metrics.RecordBlockProduced(5, 21000, 100, 0.5);

            Assert.Equal(1, metrics.BlocksProducedCount);
            Assert.Equal(100, metrics.CurrentBlockNumber);
            Assert.Equal(5, metrics.TransactionsPerBlock);
            Assert.Equal(21000, metrics.BlockGasUsed);
        }

        [Fact]
        public void SetCurrentBlockNumber_UpdatesValue()
        {
            using var metrics = new BlockProductionMetrics(ChainId);

            metrics.SetCurrentBlockNumber(999);

            Assert.Equal(999, metrics.CurrentBlockNumber);
        }
    }

    public class TxPoolMetricsTests
    {
        private const string ChainId = "420420";

        [Fact]
        public void SetPendingCount_SetsGaugeValue()
        {
            using var metrics = new TxPoolMetrics(ChainId);

            metrics.SetPendingCount(10);

            Assert.Equal(10, metrics.PendingCountValue);
        }

        [Fact]
        public void RecordTxRejected_IncrementsCounterWithReason()
        {
            using var metrics = new TxPoolMetrics(ChainId);

            metrics.RecordTxRejected("policy");
            metrics.RecordTxRejected("policy");
            metrics.RecordTxRejected("nonce");

            Assert.Equal(2, metrics.GetRejectedCount("policy"));
            Assert.Equal(1, metrics.GetRejectedCount("nonce"));
            Assert.Equal(0, metrics.GetRejectedCount("nonexistent"));
        }

        [Fact]
        public void RecordTxReceived_IncrementsCounter()
        {
            using var metrics = new TxPoolMetrics(ChainId);

            metrics.RecordTxReceived();
            metrics.RecordTxReceived();

            Assert.Equal(2, metrics.ReceivedCount);
        }
    }

    public class RpcMetricsTests
    {
        private const string ChainId = "420420";

        [Fact]
        public void MeasureRequest_ReturnsTimerAndIncrementsCounter()
        {
            using var metrics = new RpcMetrics(ChainId);

            using (var timer = metrics.MeasureRequest("eth_blockNumber"))
            {
                Assert.IsType<DurationTimer>(timer);
            }

            Assert.Equal(1, metrics.TotalRequests);
            Assert.Equal(1, metrics.GetRequestCount("eth_blockNumber"));
        }
    }

    public class SyncMetricsTests
    {
        private const string ChainId = "420420";

        [Fact]
        public void UpdateSyncStatus_SetsAllValues()
        {
            using var metrics = new SyncMetrics(ChainId);

            metrics.UpdateSyncStatus(100, 105, 95);

            Assert.Equal(100, metrics.LocalHeadValue);
            Assert.Equal(105, metrics.RemoteHeadValue);
            Assert.Equal(5, metrics.LagBlocksValue);
            Assert.Equal(95, metrics.FinalizedHeadValue);
            Assert.Equal(100, metrics.SoftHeadValue);
        }

        [Fact]
        public void SetSyncMode_TracksCurrentMode()
        {
            using var metrics = new SyncMetrics(ChainId);

            Assert.Null(metrics.CurrentMode);

            metrics.SetSyncMode("batch");
            Assert.Equal("batch", metrics.CurrentMode);

            metrics.SetSyncMode("live");
            Assert.Equal("live", metrics.CurrentMode);
        }

        [Fact]
        public void SetLocalHead_UpdatesValue()
        {
            using var metrics = new SyncMetrics(ChainId);

            metrics.SetLocalHead(500);

            Assert.Equal(500, metrics.LocalHeadValue);
        }
    }

    public class SequencerMetricsTests
    {
        private const string ChainId = "420420";

        [Fact]
        public void SetActive_SetsValues()
        {
            using var metrics = new SequencerMetrics(ChainId);

            metrics.SetActive(true, 5);
            Assert.True(metrics.IsActive);
            Assert.Equal(5, metrics.Epoch);

            metrics.SetActive(false, 0);
            Assert.False(metrics.IsActive);
        }
    }

    public class HAMetricsTests
    {
        private const string ChainId = "420420";

        [Fact]
        public void RecordTakeoverAttempt_TracksResults()
        {
            using var metrics = new HAMetrics(ChainId);

            metrics.RecordTakeoverAttempt(true);
            metrics.RecordTakeoverAttempt(false);

            Assert.Equal(1, metrics.GetTakeoverCount("success"));
            Assert.Equal(1, metrics.GetTakeoverCount("failed"));
        }

        [Fact]
        public void RecordFailover_IncrementsCounter()
        {
            using var metrics = new HAMetrics(ChainId);

            metrics.RecordFailover();
            metrics.RecordFailover();

            Assert.Equal(2, metrics.FailoversCount);
        }

        [Fact]
        public void RecordSplitBrain_IncrementsCounter()
        {
            using var metrics = new HAMetrics(ChainId);

            metrics.RecordSplitBrain();

            Assert.Equal(1, metrics.SplitBrainCount);
        }

        [Fact]
        public void UpdateDataLossWindow_CalculatesCorrectly()
        {
            using var metrics = new HAMetrics(ChainId);

            metrics.UpdateDataLossWindow(localHead: 1000, lastAnchoredBlock: 950);

            Assert.Equal(50, metrics.DataLossWindow);
        }
    }

    public class AnchoringMetricsTests
    {
        private const string ChainId = "420420";

        [Fact]
        public void RecordAnchor_UpdatesAllValues()
        {
            using var metrics = new AnchoringMetrics(ChainId);

            metrics.RecordAnchor(1000, 50000, 12.5);

            Assert.Equal(1, metrics.SubmissionsCount);
            Assert.Equal(1000, metrics.LastAnchoredBlock);
            Assert.Equal(50000, metrics.L1GasUsed);
        }

        [Fact]
        public void UpdateBatchAge_SetsValue()
        {
            using var metrics = new AnchoringMetrics(ChainId);

            metrics.UpdateBatchAge(25);

            Assert.Equal(25, metrics.BatchAgeBlocks);
        }
    }

    public class StorageMetricsTests
    {
        private const string ChainId = "420420";

        [Fact]
        public void UpdateStorageStats_SetsValues()
        {
            using var metrics = new StorageMetrics(ChainId);

            metrics.UpdateStorageStats(500, 5000);

            Assert.Equal(500, metrics.BlocksTotal);
            Assert.Equal(5000, metrics.TransactionsTotal);
        }

        [Fact]
        public void MeasureReadWrite_ReturnTimers()
        {
            using var metrics = new StorageMetrics(ChainId);

            using (var readTimer = metrics.MeasureRead("blocks"))
            {
                Assert.IsType<DurationTimer>(readTimer);
            }

            using (var writeTimer = metrics.MeasureWrite("state"))
            {
                Assert.IsType<DurationTimer>(writeTimer);
            }
        }
    }
}
