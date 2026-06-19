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
    /// </summary>
    public sealed class InMemorySnapSyncSink : ISnapSyncSink
    {
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
            _stateTrie.Put(accountHash, canonical, _storage);
            _accountCount++;
            return default;
        }

        public ValueTask BeginAccountStorageAsync(byte[] accountHash, byte[] expectedStorageRoot, CancellationToken ct)
        {
            if (_currentStorageTrie != null)
                throw new InvalidOperationException("BeginAccountStorageAsync called while another account storage scope is still open.");
            _currentStorageTrie = new PatriciaTrie();
            _currentExpectedStorageRoot = expectedStorageRoot;
            _currentAccountHash = accountHash;
            return default;
        }

        public ValueTask WriteStorageSlotAsync(byte[] slotHash, byte[] valueRlp, CancellationToken ct)
        {
            if (_currentStorageTrie == null)
                throw new InvalidOperationException("WriteStorageSlotAsync called outside a BeginAccountStorageAsync scope.");
            _currentStorageTrie.Put(slotHash, valueRlp, _storage);
            return default;
        }

        public ValueTask EndAccountStorageAsync(CancellationToken ct)
        {
            if (_currentStorageTrie == null)
                throw new InvalidOperationException("EndAccountStorageAsync called without an open scope.");
            _currentStorageTrie.SaveDirtyNodesToStorage(_storage);
            var computed = _currentStorageTrie.Root.GetHash();
            if (!ByteUtil.AreEqual(computed, _currentExpectedStorageRoot))
                throw new InvalidOperationException(
                    $"Snap storage trie for account {_currentAccountHash.ToHex()} did not match expected root {_currentExpectedStorageRoot.ToHex()} (computed {computed.ToHex()})");
            _currentStorageTrie = null;
            _currentExpectedStorageRoot = null;
            _currentAccountHash = null;
            return default;
        }

        public ValueTask WriteBytecodeAsync(byte[] codeHash, byte[] code, CancellationToken ct)
        {
            _bytecodes[codeHash.ToHex()] = code;
            return default;
        }

        public ValueTask<byte[]> FinaliseRootAsync(CancellationToken ct)
        {
            _stateTrie.SaveDirtyNodesToStorage(_storage);
            return new ValueTask<byte[]>(_stateTrie.Root.GetHash());
        }
    }
}
