using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.DevP2P.Sync.Snap.Sinks
{
    public sealed class InMemorySnapSyncSink : ISnapSyncSink
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
            => new ValueTask<IStorageScope>(new StorageScope(this, expectedStorageRoot));

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
            private readonly byte[] _expectedRoot;
            private readonly PatriciaTrie _trie;
            private bool _wroteSlots;

            public StorageScope(InMemorySnapSyncSink sink, byte[] expectedRoot)
            {
                _sink = sink;
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
                lock (_sink._lock)
                {
                    if (_wroteSlots && _expectedRoot != null && _expectedRoot.Length == 32
                        && !ByteUtil.AreEqual(_trie.Root.GetHash(), _expectedRoot))
                        throw new InvalidOperationException(
                            "Snap storage-root mismatch: the completed storage trie does not match the account's expected storage root.");
                    _trie.SaveDirtyNodesToStorage();
                }
                return default;
            }

            public ValueTask AbortAsync(CancellationToken ct) => default;
        }
    }
}
