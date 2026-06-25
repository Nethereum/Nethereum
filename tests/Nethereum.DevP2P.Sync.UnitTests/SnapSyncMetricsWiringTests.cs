using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync;
using Nethereum.DevP2P.Sync.Metrics;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    /// <summary>
    /// CRITICAL-5 coverage: SnapSyncMetrics receives RecordPhase2* and
    /// RecordPhase3* invocations from SnapSyncClient + TrieHealer. Pre-fix
    /// the recorders existed but were never called and dashboards flat-lined.
    /// </summary>
    public class SnapSyncMetricsWiringTests
    {
        private sealed class CapturingMeterListener : IDisposable
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

            // Build a 1-account state.
            var keccak = new Sha3Keccack();
            var trie = new PatriciaTrie();
            var storage = new InMemoryTrieStorage();
            var addrHash = keccak.CalculateHash(new byte[] { 0x10, 0xAA });
            var canonical = new AccountEncoder().Encode(new Account
            {
                Nonce = (EvmUInt256)1,
                Balance = (EvmUInt256)100,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
            });
            trie.Put(addrHash, canonical, storage);
            trie.SaveDirtyNodesToStorage(storage);
            var stateRoot = trie.Root.GetHash();

            var honest = new InProcessSnapPeer(new PatriciaSnapRequestHandler(storage, new InMemoryBytecodeStore()));
            var sink = new InMemorySnapSyncSink();
            var client = new SnapSyncClient(honest, sink, metrics: metrics);

            await client.SyncStateAsync(stateRoot);

            lock (listener.Counters)
            {
                // Recorded at least once. Counter additivity is enough proof
                // the wiring landed; exact count depends on chunk shape.
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
    }
}
