using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Dictionary-backed snap-sync sink. Builds the full state and storage tries in process
    /// memory, suitable for small chains (AppChain) and tests. NOT suitable for mainnet —
    /// use <c>RocksDbSnapSyncSink</c> for that.
    ///
    /// <para>
    /// Thread-safety: the <see cref="SnapSyncClient"/> runs
    /// <c>AccountConcurrency=16</c> workers in parallel against this sink.
    /// All mutating operations are guarded by a single internal lock; the
    /// open per-account scope is serialised by the client itself, so the
    /// sink only needs to defend against concurrent <see cref="WriteAccountAsync"/>
    /// + <see cref="WriteBytecodeAsync"/> + the in-scope storage-slot writes.
    /// </para>
    /// </summary>
    public sealed class InMemorySnapSyncSink : ISnapSyncSink
    {
        private readonly object _lock = new();
        private readonly InMemoryTrieStorage _storage = new();
        private readonly PatriciaTrie _stateTrie = new();
        private readonly Dictionary<string, byte[]> _bytecodes = new();
        private int _accountCount;

        private PatriciaTrie _currentStorageTrie;
        private byte[] _currentExpectedStorageRoot;
        private byte[] _currentAccountHash;

        public InMemoryTrieStorage TrieStorage => _storage;
        public PatriciaTrie StateTrie => _stateTrie;
        public IReadOnlyDictionary<string, byte[]> BytecodeByHash => _bytecodes;
        public int AccountCount => _accountCount;

        public ValueTask BeginAsync(byte[] targetRoot, CancellationToken ct) => default;

        public ValueTask WriteAccountAsync(byte[] accountHash, byte[] slimRlp, CancellationToken ct)
        {
            var canonical = SlimAccountEncoder.FromSlim(slimRlp);
            lock (_lock)
            {
                _stateTrie.Put(accountHash, canonical, _storage);
                _accountCount++;
            }
            return default;
        }

        public ValueTask BeginAccountStorageAsync(byte[] accountHash, byte[] expectedStorageRoot, CancellationToken ct)
        {
            lock (_lock)
            {
                if (_currentStorageTrie != null)
                    throw new InvalidOperationException("BeginAccountStorageAsync called while another account storage scope is still open.");
                _currentStorageTrie = new PatriciaTrie();
                _currentExpectedStorageRoot = expectedStorageRoot;
                _currentAccountHash = accountHash;
            }
            return default;
        }

        public ValueTask WriteStorageSlotAsync(byte[] slotHash, byte[] valueRlp, CancellationToken ct)
        {
            lock (_lock)
            {
                if (_currentStorageTrie == null)
                    throw new InvalidOperationException("WriteStorageSlotAsync called outside a BeginAccountStorageAsync scope.");
                _currentStorageTrie.Put(slotHash, valueRlp, _storage);
            }
            return default;
        }

        public ValueTask EndAccountStorageAsync(CancellationToken ct)
        {
            lock (_lock)
            {
                if (_currentStorageTrie == null)
                    throw new InvalidOperationException("EndAccountStorageAsync called without an open scope.");
                _currentStorageTrie.SaveDirtyNodesToStorage(_storage);
                _currentStorageTrie = null;
                _currentExpectedStorageRoot = null;
                _currentAccountHash = null;
            }
            return default;
        }

        public ValueTask AbortAccountStorageAsync(CancellationToken ct)
        {
            lock (_lock)
            {
                if (_currentStorageTrie == null)
                    throw new InvalidOperationException("AbortAccountStorageAsync called without an open scope.");
                _currentStorageTrie = null;
                _currentExpectedStorageRoot = null;
                _currentAccountHash = null;
            }
            return default;
        }

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
                _stateTrie.SaveDirtyNodesToStorage(_storage);
                return new ValueTask<byte[]>(_stateTrie.Root.GetHash());
            }
        }
    }
}
