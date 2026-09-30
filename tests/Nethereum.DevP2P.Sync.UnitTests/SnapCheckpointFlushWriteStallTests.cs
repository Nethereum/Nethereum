using System;
using System.IO;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Merkle.Patricia.Nodes;
using Nethereum.Merkle.Patricia.Storage;
using Xunit;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapCheckpointFlushWriteStallTests
    {
        private sealed class WriteStallNodeStore : ITrieNodeStore
        {
            private readonly ITrieNodeStore _inner;
            private readonly int _stallsBeforeSuccess;
            private readonly Exception _fatal;
            public int FlushAttempts;
            public int SuccessfulFlushes;

            public WriteStallNodeStore(ITrieNodeStore inner, int stallsBeforeSuccess, Exception fatal = null)
            {
                _inner = inner;
                _stallsBeforeSuccess = stallsBeforeSuccess;
                _fatal = fatal;
            }

            public void Flush()
            {
                FlushAttempts++;
                if (_fatal != null) throw _fatal;
                var stall = _stallsBeforeSuccess < 0 || FlushAttempts <= _stallsBeforeSuccess;
                if (stall)
                    throw new TransientFlushUnavailableException(
                        "simulated RocksDB write stall: writes have been stopped, unable to perform manual flush");
                _inner.Flush();
                SuccessfulFlushes++;
            }

            public void Commit(TrieNodeSet nodes) => _inner.Commit(nodes);
            public byte[] Get(Node reference) => _inner.Get(reference);
            public bool Contains(Node reference) => _inner.Contains(reference);
            public bool ContainsKey(byte[] stateRoot) => _inner.ContainsKey(stateRoot);
            public void Clear() => _inner.Clear();
        }

        private static TrieSnapSyncSink SinkOver(WriteStallNodeStore store)
            => new TrieSnapSyncSink(store, new InMemoryStateStore(), flatWriter: null);

        [Fact]
        public void Checkpoint_StallsThenRecovers_DoesNotFault_AndFlushesOnceWritesResume()
        {
            var store = new WriteStallNodeStore(new InMemoryContentNodeStore(), stallsBeforeSuccess: 2);
            var sink = SinkOver(store);

            Assert.Null(Record.Exception(() => sink.FlushAccountTrieForCheckpoint()));
            Assert.Null(Record.Exception(() => sink.FlushAccountTrieForCheckpoint()));
            Assert.Null(Record.Exception(() => sink.FlushAccountTrieForCheckpoint()));

            Assert.Equal(3, store.FlushAttempts);
            Assert.Equal(1, store.SuccessfulFlushes);
        }

        [Fact]
        public void Checkpoint_PersistentStall_NeverThrows()
        {
            var store = new WriteStallNodeStore(new InMemoryContentNodeStore(), stallsBeforeSuccess: -1);
            var sink = SinkOver(store);

            for (var i = 0; i < 5; i++)
                Assert.Null(Record.Exception(() => sink.FlushAccountTrieForCheckpoint()));

            Assert.Equal(5, store.FlushAttempts);
            Assert.Equal(0, store.SuccessfulFlushes);
        }

        [Fact]
        public void Checkpoint_RealFlushError_StillSurfaces()
        {
            var fatal = new IOException("disk failure while flushing SST");
            var store = new WriteStallNodeStore(new InMemoryContentNodeStore(), stallsBeforeSuccess: 0, fatal: fatal);
            var sink = SinkOver(store);

            var thrown = Assert.Throws<IOException>(() => sink.FlushAccountTrieForCheckpoint());
            Assert.Same(fatal, thrown);
        }
    }
}
