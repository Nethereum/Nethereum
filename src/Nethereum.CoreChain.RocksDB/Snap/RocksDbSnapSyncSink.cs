using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model.P2P.Snap;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.Snap
{
    /// <summary>
    /// Persists a snap/1 stream into the RocksDB-backed
    /// <see cref="IChainStoreBundle"/>: account + storage trie nodes go into
    /// <see cref="IChainStoreBundle.TrieNodes"/>; bytecodes go into the state
    /// store via <see cref="IStateStore.SaveCodeAsync"/> (codeHash-keyed,
    /// matching snap's wire shape).
    ///
    /// <para>
    /// This sink does NOT write account leaves into the flat
    /// <see cref="IStateStore"/> account CF. The flat store is keyed by raw
    /// 20-byte address per the post-rekey layout, while snap delivers
    /// keccak(address)-keyed leaves. The bridging read path is
    /// <c>TrieFallbackStateStore</c>, which walks the persisted state trie
    /// (keccak(address) → account) on a flat-store miss and optionally
    /// backfills.
    /// </para>
    ///
    /// <para>
    /// Per-account storage scopes validate the computed storage trie root
    /// against the expected root the snap stream advertised. The final
    /// state-root is returned to the caller; the caller is responsible for
    /// comparing it to the pivot block's stateRoot and attributing any
    /// mismatch to the offending peer.
    /// </para>
    /// </summary>
    public sealed class RocksDbSnapSyncSink : ISnapSyncSink
    {
        private readonly ITrieNodeStore _trieNodeStore;
        private readonly IStateStore _stateStore;

        private readonly PatriciaTrie _stateTrie = new();
        private int _accountCount;
        private int _slotCount;
        private int _bytecodeCount;

        private PatriciaTrie _currentStorageTrie;
        private byte[] _currentExpectedStorageRoot;
        private byte[] _currentAccountHash;

        public RocksDbSnapSyncSink(ITrieNodeStore trieNodeStore, IStateStore stateStore)
        {
            _trieNodeStore = trieNodeStore ?? throw new ArgumentNullException(nameof(trieNodeStore));
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        }

        public int AccountCount => _accountCount;
        public int SlotCount => _slotCount;
        public int BytecodeCount => _bytecodeCount;

        public ValueTask BeginAsync(byte[] targetRoot, CancellationToken ct) => default;

        public ValueTask WriteAccountAsync(byte[] accountHash, byte[] slimRlp, CancellationToken ct)
        {
            var canonical = SlimAccountEncoder.FromSlim(slimRlp);
            _stateTrie.Put(accountHash, canonical, _trieNodeStore);
            _accountCount++;
            return default;
        }

        public ValueTask BeginAccountStorageAsync(byte[] accountHash, byte[] expectedStorageRoot, CancellationToken ct)
        {
            if (_currentStorageTrie != null)
                throw new InvalidOperationException(
                    "BeginAccountStorageAsync called while another account storage scope is still open.");
            _currentStorageTrie = new PatriciaTrie();
            _currentExpectedStorageRoot = expectedStorageRoot;
            _currentAccountHash = accountHash;
            return default;
        }

        public ValueTask WriteStorageSlotAsync(byte[] slotHash, byte[] valueRlp, CancellationToken ct)
        {
            if (_currentStorageTrie == null)
                throw new InvalidOperationException(
                    "WriteStorageSlotAsync called outside a BeginAccountStorageAsync scope.");
            _currentStorageTrie.Put(slotHash, valueRlp, _trieNodeStore);
            _slotCount++;
            return default;
        }

        public ValueTask EndAccountStorageAsync(CancellationToken ct)
        {
            if (_currentStorageTrie == null)
                throw new InvalidOperationException("EndAccountStorageAsync called without an open scope.");

            _currentStorageTrie.SaveDirtyNodesToStorage(_trieNodeStore);
            var computed = _currentStorageTrie.Root.GetHash();
            if (!ByteUtil.AreEqual(computed, _currentExpectedStorageRoot))
                throw new InvalidOperationException(
                    $"Snap storage trie for account {_currentAccountHash.ToHex()} did not match expected root " +
                    $"{_currentExpectedStorageRoot.ToHex()} (computed {computed.ToHex()})");

            _currentStorageTrie = null;
            _currentExpectedStorageRoot = null;
            _currentAccountHash = null;
            return default;
        }

        public async ValueTask WriteBytecodeAsync(byte[] codeHash, byte[] code, CancellationToken ct)
        {
            await _stateStore.SaveCodeAsync(codeHash, code).ConfigureAwait(false);
            _bytecodeCount++;
        }

        public ValueTask<byte[]> FinaliseRootAsync(CancellationToken ct)
        {
            _stateTrie.SaveDirtyNodesToStorage(_trieNodeStore);
            _trieNodeStore.Flush();
            return new ValueTask<byte[]>(_stateTrie.Root.GetHash());
        }
    }
}
