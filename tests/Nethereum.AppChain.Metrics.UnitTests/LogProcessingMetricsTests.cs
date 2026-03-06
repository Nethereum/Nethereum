using System.Diagnostics.Metrics;
using System.Numerics;
using Nethereum.BlockchainProcessing.Metrics;
using Xunit;

namespace Nethereum.AppChain.Metrics.UnitTests
{
    public class LogProcessingMetricsValueTests
    {
        private const string ChainId = "420420";

        [Fact]
        public void OnBatchProcessed_UpdatesAllValues()
        {
            using var metrics = new LogProcessingMetrics(ChainId, "mud");

            metrics.OnBatchProcessed(100, 110, 25, 0.5);

            Assert.Equal(11, metrics.BlocksProcessedCount);
            Assert.Equal(25, metrics.LogsProcessedCount);
        }

        [Fact]
        public void OnBatchProcessed_AccumulatesAcrossMultipleBatches()
        {
            using var metrics = new LogProcessingMetrics(ChainId, "messaging");

            metrics.OnBatchProcessed(0, 9, 5, 0.1);
            metrics.OnBatchProcessed(10, 19, 3, 0.2);

            Assert.Equal(20, metrics.BlocksProcessedCount);
            Assert.Equal(8, metrics.LogsProcessedCount);
        }

        [Fact]
        public void OnBatchProcessed_ZeroLogs_DoesNotIncrementLogCount()
        {
            using var metrics = new LogProcessingMetrics(ChainId, "mud");

            metrics.OnBatchProcessed(0, 99, 0, 0.3);

            Assert.Equal(100, metrics.BlocksProcessedCount);
            Assert.Equal(0, metrics.LogsProcessedCount);
        }

        [Fact]
        public void OnBlockProgressUpdated_SetsLastProcessedBlock()
        {
            using var metrics = new LogProcessingMetrics(ChainId, "mud");

            metrics.OnBlockProgressUpdated(500);

            Assert.Equal(500, metrics.LastProcessedBlock);
        }

        [Fact]
        public void SetChainHead_SetsValue()
        {
            using var metrics = new LogProcessingMetrics(ChainId, "mud");

            metrics.SetChainHead(1000);

            Assert.Equal(1000, metrics.ChainHead);
        }
    }

    public class MeterListenerLogProcessingTests
    {
        [Fact]
        public void OnBatchProcessed_EmitsBlockCounterAndDurationHistogram()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("LP1");
            using var metrics = new LogProcessingMetrics("420420", "mud", "LP1");

            metrics.OnBatchProcessed(0, 9, 5, 0.25);

            var blockCounter = collector.LongMeasurements
                .First(m => m.Name == "logprocessing.blocks.processed");
            Assert.Equal(10, blockCounter.Value);
            Assert.Equal("420420", blockCounter.Tags["chain_id"]);
            Assert.Equal("LP1", blockCounter.Tags["chain_name"]);
            Assert.Equal("mud", blockCounter.Tags["processor_type"]);

            var logCounter = collector.LongMeasurements
                .First(m => m.Name == "logprocessing.logs.processed");
            Assert.Equal(5, logCounter.Value);

            var histogram = collector.DoubleMeasurements
                .First(m => m.Name == "logprocessing.batch.duration");
            Assert.Equal(0.25, histogram.Value, 3);
            Assert.Equal("mud", histogram.Tags["processor_type"]);
        }

        [Fact]
        public void OnError_EmitsErrorCounterWithReasonTag()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("LP2");
            using var metrics = new LogProcessingMetrics("420420", "messaging", "LP2");

            metrics.OnError("RpcClientException");

            var counter = collector.LongMeasurements
                .First(m => m.Name == "logprocessing.errors");
            Assert.Equal(1, counter.Value);
            Assert.Equal("RpcClientException", counter.Tags["reason"]);
            Assert.Equal("messaging", counter.Tags["processor_type"]);
        }

        [Fact]
        public void OnReorgDetected_EmitsReorgCounter()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("LP3");
            using var metrics = new LogProcessingMetrics("420420", "mud", "LP3");

            metrics.OnReorgDetected(95, 100);

            var counter = collector.LongMeasurements
                .First(m => m.Name == "logprocessing.reorgs");
            Assert.Equal(1, counter.Value);
            Assert.Equal("mud", counter.Tags["processor_type"]);
        }

        [Fact]
        public void OnGetLogsRetry_EmitsRetryCounter()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("LP4");
            using var metrics = new LogProcessingMetrics("420420", "custom", "LP4");

            metrics.OnGetLogsRetry(3);

            var counter = collector.LongMeasurements
                .First(m => m.Name == "logprocessing.getlogs.retries");
            Assert.Equal(1, counter.Value);
            Assert.Equal("3", counter.Tags["retry_number"]);
        }

        [Fact]
        public void ObservableGauges_EmitLastBlockAndLag()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("LP5");
            using var metrics = new LogProcessingMetrics("420420", "mud", "LP5");

            metrics.SetChainHead(1000);
            metrics.OnBlockProgressUpdated(950);
            collector.Clear();
            listener.RecordObservableInstruments();

            var lastBlock = collector.LongMeasurements
                .First(m => m.Name == "logprocessing.last_block");
            Assert.Equal(950, lastBlock.Value);
            Assert.Equal("mud", lastBlock.Tags["processor_type"]);

            var lag = collector.LongMeasurements
                .First(m => m.Name == "logprocessing.lag");
            Assert.Equal(50, lag.Value);
        }

        [Fact]
        public void ObservableGauges_LagIsZeroWhenCaughtUp()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("LP6");
            using var metrics = new LogProcessingMetrics("420420", "messaging", "LP6");

            metrics.SetChainHead(500);
            metrics.OnBlockProgressUpdated(500);
            collector.Clear();
            listener.RecordObservableInstruments();

            var lag = collector.LongMeasurements
                .First(m => m.Name == "logprocessing.lag");
            Assert.Equal(0, lag.Value);
        }

        [Fact]
        public void ProcessorType_FlowsToAllInstruments()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("LP7");
            using var metrics = new LogProcessingMetrics("999", "custom_indexer", "LP7");

            metrics.OnBatchProcessed(0, 0, 1, 0.01);
            metrics.OnError("timeout");
            metrics.OnReorgDetected(0, 1);
            metrics.OnGetLogsRetry(1);

            foreach (var m in collector.LongMeasurements)
            {
                Assert.Equal("custom_indexer", m.Tags["processor_type"]);
            }
        }
    }
}
