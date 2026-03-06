using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using Nethereum.AppChain.Anchoring.Metrics;
using Nethereum.AppChain.Sequencer.Metrics;
using Nethereum.AppChain.Sync.Metrics;
using Nethereum.CoreChain.Metrics;
using Xunit;

namespace Nethereum.AppChain.Metrics.UnitTests
{
    public class MeterListenerBlockProductionTests
    {
        [Fact]
        public void RecordBlockProduced_EmitsCounterAndHistogramWithTags()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("BP1");
            using var metrics = new BlockProductionMetrics("420420", "BP1");

            metrics.RecordBlockProduced(5, 21000, 100, 0.5);

            var counter = collector.LongMeasurements
                .First(m => m.Name == "corechain.block.produced");
            Assert.Equal(1, counter.Value);
            Assert.Equal("420420", counter.Tags["chain_id"]);
            Assert.Equal("BP1", counter.Tags["chain_name"]);

            var histogram = collector.DoubleMeasurements
                .First(m => m.Name == "corechain.block.production.duration");
            Assert.Equal(0.5, histogram.Value, 3);
            Assert.Equal("420420", histogram.Tags["chain_id"]);
            Assert.Equal("BP1", histogram.Tags["chain_name"]);
        }

        [Fact]
        public void RecordError_EmitsErrorCounterWithReasonTag()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("BP2");
            using var metrics = new BlockProductionMetrics("420420", "BP2");

            metrics.RecordError("oom");

            var counter = collector.LongMeasurements
                .First(m => m.Name == "corechain.block.production.errors");
            Assert.Equal(1, counter.Value);
            Assert.Equal("oom", counter.Tags["reason"]);
            Assert.Equal("420420", counter.Tags["chain_id"]);
        }

        [Fact]
        public void ObservableGauges_EmitBlockNumberAndTxCountAndGas()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("BP3");
            using var metrics = new BlockProductionMetrics("420420", "BP3");

            metrics.RecordBlockProduced(5, 21000, 100, 0.5);
            collector.Clear();
            listener.RecordObservableInstruments();

            var blockNum = collector.LongMeasurements
                .First(m => m.Name == "corechain.block.number");
            Assert.Equal(100, blockNum.Value);

            var txCount = collector.IntMeasurements
                .First(m => m.Name == "corechain.block.transactions");
            Assert.Equal(5, txCount.Value);

            var gasUsed = collector.LongMeasurements
                .First(m => m.Name == "corechain.block.gas_used");
            Assert.Equal(21000, gasUsed.Value);
        }
    }

    public class MeterListenerTxPoolTests
    {
        [Fact]
        public void RecordTxReceived_EmitsReceivedCounter()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("TP1");
            using var metrics = new TxPoolMetrics("420420", "TP1");

            metrics.RecordTxReceived();

            var counter = collector.LongMeasurements
                .First(m => m.Name == "corechain.txpool.received");
            Assert.Equal(1, counter.Value);
            Assert.Equal("420420", counter.Tags["chain_id"]);
            Assert.Equal("TP1", counter.Tags["chain_name"]);
        }

        [Fact]
        public void RecordTxRejected_EmitsCounterWithReasonTag()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("TP2");
            using var metrics = new TxPoolMetrics("420420", "TP2");

            metrics.RecordTxRejected("nonce");

            var counter = collector.LongMeasurements
                .First(m => m.Name == "corechain.txpool.rejected");
            Assert.Equal(1, counter.Value);
            Assert.Equal("nonce", counter.Tags["reason"]);
        }

        [Fact]
        public void ObservableGauges_EmitPendingAndQueued()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("TP3");
            using var metrics = new TxPoolMetrics("420420", "TP3");

            metrics.SetPendingCount(10);
            metrics.SetQueuedCount(3);
            listener.RecordObservableInstruments();

            var pending = collector.IntMeasurements
                .First(m => m.Name == "corechain.txpool.pending");
            Assert.Equal(10, pending.Value);

            var queued = collector.IntMeasurements
                .First(m => m.Name == "corechain.txpool.queued");
            Assert.Equal(3, queued.Value);
        }
    }

    public class MeterListenerRpcTests
    {
        [Fact]
        public void MeasureRequest_EmitsCounterAndRecordsDuration()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("RPC1");
            using var metrics = new RpcMetrics("420420", "RPC1");

            using (var timer = metrics.MeasureRequest("eth_blockNumber"))
            {
                Thread.Sleep(10);
            }

            var counter = collector.LongMeasurements
                .First(m => m.Name == "corechain.rpc.requests");
            Assert.Equal(1, counter.Value);
            Assert.Equal("eth_blockNumber", counter.Tags["method"]);

            var histogram = collector.DoubleMeasurements
                .First(m => m.Name == "corechain.rpc.request.duration");
            Assert.True(histogram.Value > 0, "DurationTimer should record non-zero elapsed time");
            Assert.Equal("eth_blockNumber", histogram.Tags["method"]);
        }

        [Fact]
        public void RecordError_EmitsErrorCounterWithMethodAndCode()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("RPC2");
            using var metrics = new RpcMetrics("420420", "RPC2");

            metrics.RecordError("eth_call", -32603);

            var counter = collector.LongMeasurements
                .First(m => m.Name == "corechain.rpc.errors");
            Assert.Equal(1, counter.Value);
            Assert.Equal("eth_call", counter.Tags["method"]);
            Assert.Equal("-32603", counter.Tags["code"]);
        }
    }

    public class MeterListenerStorageTests
    {
        [Fact]
        public void MeasureReadWrite_EmitsHistogramsWithStoreTags()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("ST1");
            using var metrics = new StorageMetrics("420420", "ST1");

            using (metrics.MeasureRead("blocks")) { }
            using (metrics.MeasureWrite("state")) { }

            var readHist = collector.DoubleMeasurements
                .First(m => m.Name == "corechain.storage.read.duration");
            Assert.Equal("blocks", readHist.Tags["store"]);
            Assert.True(readHist.Value >= 0);

            var writeHist = collector.DoubleMeasurements
                .First(m => m.Name == "corechain.storage.write.duration");
            Assert.Equal("state", writeHist.Tags["store"]);
            Assert.True(writeHist.Value >= 0);
        }

        [Fact]
        public void ObservableGauges_EmitBlocksAndTransactions()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("ST2");
            using var metrics = new StorageMetrics("420420", "ST2");

            metrics.UpdateStorageStats(500, 5000);
            listener.RecordObservableInstruments();

            var blocks = collector.LongMeasurements
                .First(m => m.Name == "corechain.storage.blocks");
            Assert.Equal(500, blocks.Value);

            var txns = collector.LongMeasurements
                .First(m => m.Name == "corechain.storage.transactions");
            Assert.Equal(5000, txns.Value);
        }
    }

    public class MeterListenerSequencerTests
    {
        [Fact]
        public void RecordPolicyRejection_EmitsCounterWithPolicyTag()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("SQ1");
            using var metrics = new SequencerMetrics("420420", "SQ1");

            metrics.RecordPolicyRejection("allowlist");

            var counter = collector.LongMeasurements
                .First(m => m.Name == "sequencer.policy_rejections");
            Assert.Equal(1, counter.Value);
            Assert.Equal("allowlist", counter.Tags["policy"]);
        }

        [Fact]
        public void ObservableGauges_EmitActiveAndEpoch()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("SQ2");
            using var metrics = new SequencerMetrics("420420", "SQ2");

            metrics.SetActive(true, 5);
            listener.RecordObservableInstruments();

            var active = collector.IntMeasurements
                .First(m => m.Name == "sequencer.active");
            Assert.Equal(1, active.Value);

            var epoch = collector.LongMeasurements
                .First(m => m.Name == "sequencer.epoch");
            Assert.Equal(5, epoch.Value);
        }
    }

    public class MeterListenerHATests
    {
        [Fact]
        public void RecordFailover_EmitsFailoverCounter()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("HA1");
            using var metrics = new HAMetrics("420420", "HA1");

            metrics.RecordFailover();

            var counter = collector.LongMeasurements
                .First(m => m.Name == "sequencer.ha.failovers");
            Assert.Equal(1, counter.Value);
            Assert.Equal("420420", counter.Tags["chain_id"]);
            Assert.Equal("HA1", counter.Tags["chain_name"]);
        }

        [Fact]
        public void RecordSplitBrain_EmitsCounter()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("HA2");
            using var metrics = new HAMetrics("420420", "HA2");

            metrics.RecordSplitBrain();

            var counter = collector.LongMeasurements
                .First(m => m.Name == "sequencer.ha.split_brain_detected");
            Assert.Equal(1, counter.Value);
        }

        [Fact]
        public void RecordHeartbeat_EmitsHistogramAndUpdatesHealth()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("HA3");
            using var metrics = new HAMetrics("420420", "HA3");

            metrics.RecordHeartbeat(0.025, true);

            var histogram = collector.DoubleMeasurements
                .First(m => m.Name == "sequencer.ha.heartbeat.duration");
            Assert.Equal(0.025, histogram.Value, 3);

            listener.RecordObservableInstruments();

            var healthy = collector.IntMeasurements
                .First(m => m.Name == "sequencer.ha.primary_healthy");
            Assert.Equal(1, healthy.Value);
        }
    }

    public class MeterListenerSyncTests
    {
        [Fact]
        public void RecordBatchImport_EmitsCounterAndHistogram()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("SY1");
            using var metrics = new SyncMetrics("420420", "SY1");

            metrics.RecordBatchImport(1024, 0.15);

            var counter = collector.LongMeasurements
                .First(m => m.Name == "sync.batch.imports");
            Assert.Equal(1, counter.Value);

            var histogram = collector.DoubleMeasurements
                .First(m => m.Name == "sync.batch.import.duration");
            Assert.Equal(0.15, histogram.Value, 3);
        }

        [Fact]
        public void ObservableGauges_EmitSyncStatus()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("SY2");
            using var metrics = new SyncMetrics("420420", "SY2");

            metrics.UpdateSyncStatus(100, 105, 95);
            listener.RecordObservableInstruments();

            var localHead = collector.LongMeasurements
                .First(m => m.Name == "sync.local_head");
            Assert.Equal(100, localHead.Value);

            var remoteHead = collector.LongMeasurements
                .First(m => m.Name == "sync.remote_head");
            Assert.Equal(105, remoteHead.Value);

            var lag = collector.LongMeasurements
                .First(m => m.Name == "sync.lag");
            Assert.Equal(5, lag.Value);
        }
    }

    public class MeterListenerAnchoringTests
    {
        [Fact]
        public void RecordAnchor_EmitsCounterAndHistogram()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("AN1");
            using var metrics = new AnchoringMetrics("420420", "AN1");

            metrics.RecordAnchor(1000, 50000, 12.5);

            var counter = collector.LongMeasurements
                .First(m => m.Name == "anchoring.submissions");
            Assert.Equal(1, counter.Value);

            var histogram = collector.DoubleMeasurements
                .First(m => m.Name == "anchoring.confirmation.duration");
            Assert.Equal(12.5, histogram.Value, 3);
        }

        [Fact]
        public void ObservableGauges_EmitLastBlockAndGas()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("AN2");
            using var metrics = new AnchoringMetrics("420420", "AN2");

            metrics.RecordAnchor(1000, 50000, 12.5);
            collector.Clear();
            listener.RecordObservableInstruments();

            var lastBlock = collector.LongMeasurements
                .First(m => m.Name == "anchoring.last_block");
            Assert.Equal(1000, lastBlock.Value);

            var gasUsed = collector.LongMeasurements
                .First(m => m.Name == "anchoring.l1_gas_used");
            Assert.Equal(50000, gasUsed.Value);
        }
    }

    public class MeterListenerCrossCuttingTests
    {
        [Fact]
        public void CustomName_FlowsToMeterNameAndTags()
        {
            var collector = new MeasurementCollector();
            var meterNames = new List<string>();

            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name.StartsWith("MyChain."))
                {
                    meterNames.Add(instrument.Meter.Name);
                    meterListener.EnableMeasurementEvents(instrument);
                }
            };
            listener.SetMeasurementEventCallback<long>(collector.CaptureLong);
            listener.SetMeasurementEventCallback<double>(collector.CaptureDouble);
            listener.Start();

            using var metrics = new BlockProductionMetrics("999", "MyChain");
            metrics.RecordBlockProduced(1, 1000, 50, 0.1);

            Assert.Contains("MyChain.CoreChain", meterNames);
            Assert.Contains("MyChain.CoreChain.Detailed", meterNames);

            var counter = collector.LongMeasurements
                .First(m => m.Name == "corechain.block.produced");
            Assert.Equal("MyChain", counter.Tags["chain_name"]);
            Assert.Equal("999", counter.Tags["chain_id"]);
        }

        [Fact]
        public void DurationTimer_RecordsNonZeroElapsedTime()
        {
            var collector = new MeasurementCollector();
            using var listener = collector.CreateListener("DT1");
            using var metrics = new RpcMetrics("420420", "DT1");

            using (metrics.MeasureRequest("eth_getBalance"))
            {
                Thread.Sleep(15);
            }

            var histogram = collector.DoubleMeasurements
                .First(m => m.Name == "corechain.rpc.request.duration");
            Assert.True(histogram.Value >= 0.010,
                $"Expected >= 10ms elapsed, got {histogram.Value:F4}s");
        }
    }

    internal class MeasurementCollector
    {
        public List<(string Name, long Value, Dictionary<string, object?> Tags)> LongMeasurements { get; } = new();
        public List<(string Name, double Value, Dictionary<string, object?> Tags)> DoubleMeasurements { get; } = new();
        public List<(string Name, int Value, Dictionary<string, object?> Tags)> IntMeasurements { get; } = new();

        public void CaptureLong(Instrument instrument, long measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            LongMeasurements.Add((instrument.Name, measurement, CopyTags(tags)));
        }

        public void CaptureDouble(Instrument instrument, double measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            DoubleMeasurements.Add((instrument.Name, measurement, CopyTags(tags)));
        }

        public void CaptureInt(Instrument instrument, int measurement,
            ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            IntMeasurements.Add((instrument.Name, measurement, CopyTags(tags)));
        }

        public MeterListener CreateListener(string namePrefix)
        {
            var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name.StartsWith(namePrefix + "."))
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            };
            listener.SetMeasurementEventCallback<long>(CaptureLong);
            listener.SetMeasurementEventCallback<double>(CaptureDouble);
            listener.SetMeasurementEventCallback<int>(CaptureInt);
            listener.Start();
            return listener;
        }

        public void Clear()
        {
            LongMeasurements.Clear();
            DoubleMeasurements.Clear();
            IntMeasurements.Clear();
        }

        private static Dictionary<string, object?> CopyTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var tag in tags)
                dict[tag.Key] = tag.Value;
            return dict;
        }
    }
}
