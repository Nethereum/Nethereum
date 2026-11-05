using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.DevP2P.Sync.Snap.Sinks
{
    public sealed class InMemorySnapSyncSink : ISnapSyncSink, IStorageRootSource
    {
        private readonly object _lock = new();
        private readonly InMemoryContentNodeStore _storage = new();
        private readonly PatriciaTrie _stateTrie;

        public InMemorySnapSyncSink()
        {
            _stateTrie = new PatriciaTrie(_storage);
        }
        private readonly Dictionary<string, byte[]> _bytecodes = new();
        private int _accountCount;

        private System.Action<byte[], byte[], byte[]> _onStorageRootCompleted;

        System.Action<byte[], byte[], byte[]> IStorageRootSource.OnStorageRootCompleted
        {
            get => _onStorageRootCompleted;
            set => _onStorageRootCompleted = value;
        }

        internal InMemoryContentNodeStore NodeStore => _storage;

        public InMemoryContentNodeStore TrieStorage => _storage;
        public PatriciaTrie StateTrie => _stateTrie;
        public IReadOnlyDictionary<string, byte[]> BytecodeByHash => _bytecodes;
        public int AccountCount => _accountCount;

        public ValueTask BeginAsync(byte[] targetRoot, CancellationToken ct) => default;

        public ValueTask WriteAccountAsync(byte[] accountHash, byte[] slimRlp, CancellationToken ct)
        {
            var canonical = SlimAccountEncoder.FromSlim(slimRlp);
            lock (_lock)
            {
                _stateTrie.Put(accountHash, canonical);
                _accountCount++;
            }
            return default;
        }

        public ValueTask<IStorageScope> BeginAccountStorageAsync(
            byte[] accountHash, byte[] expectedStorageRoot, CancellationToken ct)
            => new ValueTask<IStorageScope>(new StorageScope(this, accountHash, expectedStorageRoot));

        public ValueTask WriteBytecodeAsync(byte[] codeHash, byte[] code, CancellationToken ct)
        {
            lock (_lock)
            {
                _bytecodes[codeHash.ToHex()] = code;
            }
            return default;
        }

        public ValueTask<byte[]> FinaliseRootAsync(CancellationToken ct)
        {
            lock (_lock)
            {
                _stateTrie.SaveDirtyNodesToStorage();
                return new ValueTask<byte[]>(_stateTrie.Root.GetHash());
            }
        }

        private sealed class StorageScope : IStorageScope
        {
            private readonly InMemorySnapSyncSink _sink;
            private readonly byte[] _accountHash;
            private readonly byte[] _expectedRoot;
            private readonly PatriciaTrie _trie;
            private bool _wroteSlots;

            public StorageScope(InMemorySnapSyncSink sink, byte[] accountHash, byte[] expectedRoot)
            {
                _sink = sink;
                _accountHash = accountHash;
                _expectedRoot = expectedRoot;
                _trie = new PatriciaTrie(sink._storage);
            }

            public ValueTask WriteSlotAsync(byte[] slotHash, byte[] valueRlp, CancellationToken ct)
            {
                lock (_sink._lock)
                {
                    _trie.Put(slotHash, valueRlp);
                }
                _wroteSlots = true;
                return default;
            }

            public ValueTask EndAsync(CancellationToken ct)
            {
                byte[] completedRoot;
                lock (_sink._lock)
                {
                    completedRoot = _wroteSlots ? _trie.Root.GetHash() : null;
                    _trie.SaveDirtyNodesToStorage();
                }
                _sink._onStorageRootCompleted?.Invoke(_accountHash, completedRoot, _expectedRoot);
                return default;
            }

            public ValueTask AbortAsync(CancellationToken ct) => default;
        }
    }
}
