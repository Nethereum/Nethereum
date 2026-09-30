using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapSyncMetricsWiringTests
    {
        internal sealed class CapturingMeterListener : IDisposable
        {
            private readonly MeterListener _listener;
            public Dictionary<string, long> Counters { get; } = new();

            public CapturingMeterListener(string meterPrefix)
            {
                _listener = new MeterListener
                {
                    InstrumentPublished = (instrument, l) =>
                    {
                        if (instrument.Meter.Name.StartsWith(meterPrefix, StringComparison.Ordinal))
                            l.EnableMeasurementEvents(instrument);
                    }
                };
                _listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
                {
                    lock (Counters)
                    {
                        Counters[instrument.Name] = Counters.GetValueOrDefault(instrument.Name) + value;
                    }
                });
                _listener.Start();
            }

            public void Dispose() => _listener.Dispose();
        }

        private sealed class InMemoryBytecodeStore : IBytecodeStore
        {
            private readonly Dictionary<byte[], byte[]> _codes = new(ByteArrayComparer.Current);
            public void Put(byte[] hash, byte[] code) { _codes[hash] = code; }
            public byte[] Get(byte[] hash) => _codes.TryGetValue(hash, out var v) ? v : null;
        }

        [Fact]
        public async Task SyncStateAsync_RecordsPhase2AccountsSynced()
        {
            using var listener = new CapturingMeterListener("Nethereum-Test.SnapSync");
            using var metrics = new SnapSyncMetrics("Nethereum-Test");

            var keccak = new Sha3Keccack();
            var storage = new InMemoryContentNodeStore();
            var trie = new PatriciaTrie(storage);
            var addrHash = keccak.CalculateHash(new byte[] { 0x10, 0xAA });
            var canonical = new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)100,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            });
            trie.Put(addrHash, canonical);
            trie.SaveDirtyNodesToStorage();
            var stateRoot = trie.Root.GetHash();

            var honest = new InProcessSnapPeer(new PatriciaSnapRequestHandler(storage, new InMemoryBytecodeStore()));
            var sink = new InMemorySnapSyncSink();
            var client = new SnapSyncClient(honest, sink, metrics: metrics);

            await client.SyncStateAsync(stateRoot);

            lock (listener.Counters)
            {
                Assert.True(listener.Counters.GetValueOrDefault("snap.phase2.accounts.synced") >= 1,
                    "expected snap.phase2.accounts.synced to be incremented");
            }
        }

        [Fact]
        public void RecordResume_FiresResumeCounter()
        {
            using var listener = new CapturingMeterListener("Nethereum-Test.SnapSync");
            using var metrics = new SnapSyncMetrics("Nethereum-Test");

            metrics.RecordResume(SnapPhase.Phase2Running);

            lock (listener.Counters)
            {
                Assert.Equal(1L, listener.Counters.GetValueOrDefault("snap.resume.total"));
            }
        }

        [Fact]
        public void RecordPhase3PivotRotation_FiresRotationCounter()
        {
            using var listener = new CapturingMeterListener("Nethereum-Test.SnapSync");
            using var metrics = new SnapSyncMetrics("Nethereum-Test");

            metrics.RecordPhase3PivotRotation();
            metrics.RecordPhase3PivotRotation();

            lock (listener.Counters)
            {
                Assert.Equal(2L, listener.Counters.GetValueOrDefault("snap.phase3.pivot.rotations"));
            }
        }

        [Fact]
        public void RecordPhase3RecycleNoProgress_FiresCounter()
        {
            using var listener = new CapturingMeterListener("Nethereum-Test.SnapSync");
            using var metrics = new SnapSyncMetrics("Nethereum-Test");

            metrics.RecordPhase3RecycleNoProgress();
            metrics.RecordPhase3RecycleNoProgress();

            lock (listener.Counters)
            {
                Assert.Equal(2L, listener.Counters.GetValueOrDefault("snap.phase3.recycle.no_progress"));
            }
        }

        [Fact]
        public void RecordPhase3NodesHealed_FiresHealCounter()
        {
            using var listener = new CapturingMeterListener("Nethereum-Test.SnapSync");
            using var metrics = new SnapSyncMetrics("Nethereum-Test");

            metrics.RecordPhase3NodesHealed(50);

            lock (listener.Counters)
            {
                Assert.Equal(50L, listener.Counters.GetValueOrDefault("snap.phase3.nodes.healed"));
            }
        }

        [Fact]
        public void RecordPhase3NodesHealed_AccumulatesReadableRunningTotal()
        {
            using var metrics = new SnapSyncMetrics("Nethereum-Test");

            Assert.Equal(0L, metrics.Phase3NodesHealedTotal);
            metrics.RecordPhase3NodesHealed(50);
            metrics.RecordPhase3NodesHealed(30);
            Assert.Equal(80L, metrics.Phase3NodesHealedTotal);
        }

        [Fact]
        public void RecordPhase3BytecodesHealed_AccumulatesReadableRunningTotal()
        {
            using var listener = new CapturingMeterListener("Nethereum-Test.SnapSync");
            using var metrics = new SnapSyncMetrics("Nethereum-Test");

            Assert.Equal(0L, metrics.Phase3BytecodesHealedTotal);
            metrics.RecordPhase3BytecodesHealed(4);
            metrics.RecordPhase3BytecodesHealed(1);
            Assert.Equal(5L, metrics.Phase3BytecodesHealedTotal);

            lock (listener.Counters)
            {
                Assert.Equal(5L, listener.Counters.GetValueOrDefault("snap.phase3.bytecodes.healed"));
            }
        }
    }
}
