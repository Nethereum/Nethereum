using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.CoreChain.State
{
    /// <summary>
    /// Read-path decorator that resolves accounts and storage slots via the
    /// persisted Patricia state trie (keccak(address) → account; keccak(slot)
    /// → value) when the flat <see cref="IStateStore"/> has nothing — the
    /// post-snap-bootstrap scenario where snap leaves were written into
    /// <see cref="ITrieNodeStore"/> but the address-keyed flat CF is still
    /// empty for cold-read addresses.
    ///
    /// <para>
    /// Behaviour:
    /// <list type="bullet">
    ///   <item><c>GetAccountAsync</c>: try inner; on null walk the state trie
    ///         at the current head's <c>StateRoot</c> using <c>keccak(address)</c>;
    ///         decode; optionally backfill the inner store; return.</item>
    ///   <item><c>GetStorageAsync</c>: try inner; on null fall back to the
    ///         per-account storage trie at the account's <c>StateRoot</c> using
    ///         <c>keccak(slot)</c>; optionally backfill; return.</item>
    ///   <item>All write methods pass through to the inner store unchanged —
    ///         new accounts/slots produced by block execution take the standard
    ///         path; only cold reads of pre-existing snap state are mediated
    ///         by this decorator.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// The <c>stateRootProvider</c> returns the canonical state root the
    /// caller wants reads to be relative to — typically the latest committed
    /// block's <c>BlockHeader.StateRoot</c>. The decorator does NOT cache this
    /// value: every call re-evaluates so the head can advance between calls.
    /// </para>
    /// </summary>
    public sealed class TrieFallbackStateStore : IStateStore
    {
        private readonly IStateStore _inner;
        private readonly ITrieStorage _trieStorage;
        private readonly Func<byte[]> _stateRootProvider;
        private readonly bool _backfill;
        private readonly IHashProvider _hashProvider;

        public TrieFallbackStateStore(
            IStateStore inner,
            ITrieStorage trieStorage,
            Func<byte[]> stateRootProvider,
            bool backfill = true,
            IHashProvider hashProvider = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _trieStorage = trieStorage ?? throw new ArgumentNullException(nameof(trieStorage));
            _stateRootProvider = stateRootProvider ?? throw new ArgumentNullException(nameof(stateRootProvider));
            _backfill = backfill;
            _hashProvider = hashProvider ?? new Sha3KeccackHashProvider();
        }

        public async Task<Account> GetAccountAsync(string address)
        {
            var fromInner = await _inner.GetAccountAsync(address).ConfigureAwait(false);
            if (fromInner != null) return fromInner;

            var stateRoot = _stateRootProvider();
            if (IsEmptyOrAllZero(stateRoot)) return null;

            var addrKey = _hashProvider.ComputeHash(AddressBytes(address));
            var trie = new PatriciaTrie(stateRoot, _hashProvider);
            var rlp = trie.Get(addrKey, _trieStorage);
            if (rlp == null || rlp.Length == 0) return null;

            var account = AccountEncoder.Current.Decode(rlp);
            if (_backfill)
                await _inner.SaveAccountAsync(address, account).ConfigureAwait(false);
            return account;
        }

        public async Task<byte[]> GetStorageAsync(string address, BigInteger slot)
        {
            var fromInner = await _inner.GetStorageAsync(address, slot).ConfigureAwait(false);
            if (fromInner != null && fromInner.Length > 0) return fromInner;

            var account = await GetAccountAsync(address).ConfigureAwait(false);
            if (account == null) return null;
            if (account.StateRoot == null || IsEmptyOrAllZero(account.StateRoot)
                || ByteUtil.AreEqual(account.StateRoot, DefaultValues.EMPTY_TRIE_HASH))
                return null;

            // StateKeys.StorageSlotKey already keccak-hashes the slot; the
            // Patricia storage trie keys leaves by that hash directly (Yellow
            // Paper §4.1) — do NOT re-hash here.
            var slotKey = StateKeys.StorageSlotKey(slot);
            var storageTrie = new PatriciaTrie(account.StateRoot, _hashProvider);
            var trieValue = storageTrie.Get(slotKey, _trieStorage);
            if (trieValue == null || trieValue.Length == 0) return null;

            // Storage-trie leaves are RLP(rawSlotValue); unwrap so we return
            // the same shape the inner IStateStore.GetStorageAsync produces.
            var raw = ((RLPItem)RLP.RLP.Decode(trieValue))?.RLPData ?? trieValue;

            if (_backfill && raw != null && raw.Length > 0)
                await _inner.SaveStorageAsync(address, slot, raw).ConfigureAwait(false);
            return raw;
        }

        public async Task<bool> AccountExistsAsync(string address)
        {
            if (await _inner.AccountExistsAsync(address).ConfigureAwait(false)) return true;
            return await GetAccountAsync(address).ConfigureAwait(false) != null;
        }

        public Task SaveAccountAsync(string address, Account account) => _inner.SaveAccountAsync(address, account);
        public Task DeleteAccountAsync(string address) => _inner.DeleteAccountAsync(address);
        public Task<Dictionary<string, Account>> GetAllAccountsAsync() => _inner.GetAllAccountsAsync();
        public IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync() => _inner.StreamAccountsAsync();
        public Task SaveStorageAsync(string address, BigInteger slot, byte[] value)
            => _inner.SaveStorageAsync(address, slot, value);
        public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value)
            => _inner.SaveStorageByKeccakAsync(address, slotKeccak, value);
        public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address) => _inner.GetAllStorageAsync(address);
        public Task ClearStorageAsync(string address) => _inner.ClearStorageAsync(address);
        public Task<byte[]> GetCodeAsync(byte[] codeHash) => _inner.GetCodeAsync(codeHash);
        public Task SaveCodeAsync(byte[] codeHash, byte[] code) => _inner.SaveCodeAsync(codeHash, code);
        public Task<IStateSnapshot> CreateSnapshotAsync() => _inner.CreateSnapshotAsync();
        public Task CommitSnapshotAsync(IStateSnapshot snapshot) => _inner.CommitSnapshotAsync(snapshot);
        public Task RevertSnapshotAsync(IStateSnapshot snapshot) => _inner.RevertSnapshotAsync(snapshot);
        public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync() => _inner.GetDirtyAccountAddressesAsync();
        public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address)
            => _inner.GetDirtyStorageSlotsAsync(address);
        public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync()
            => _inner.GetStorageClearedAddressesAsync();
        public Task ClearDirtyTrackingAsync() => _inner.ClearDirtyTrackingAsync();

        private static byte[] AddressBytes(string address)
            => AddressUtil.Current.ConvertToValid20ByteAddress(address).HexToByteArray();

        private static bool IsEmptyOrAllZero(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return true;
            for (int i = 0; i < bytes.Length; i++)
                if (bytes[i] != 0) return false;
            return true;
        }
    }
}
