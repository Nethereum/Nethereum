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
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Merkle.Patricia.Proofs;

namespace Nethereum.CoreChain.State
{
    public sealed class TrieFallbackStateStore : IStateStore
    {
        private readonly IStateStore _inner;
        public IStateStore Inner => _inner;
        private readonly INodeBlobStore _trieStorage;
        public INodeBlobStore TrieStorage => _trieStorage;
        private readonly ITrieNodeStore _nodeStore;
        private readonly Func<byte[]> _stateRootProvider;
        public Func<byte[]> StateRootProvider => _stateRootProvider;
        private readonly bool _backfill;
        private readonly IHashProvider _hashProvider;

        public TrieFallbackStateStore(
            IStateStore inner,
            INodeBlobStore trieStorage,
            Func<byte[]> stateRootProvider,
            bool backfill = true,
            IHashProvider hashProvider = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _trieStorage = trieStorage ?? throw new ArgumentNullException(nameof(trieStorage));
            _nodeStore = ContentAddressedNodeStore.Wrap(trieStorage);
            _stateRootProvider = stateRootProvider ?? throw new ArgumentNullException(nameof(stateRootProvider));
            _backfill = backfill;
            _hashProvider = hashProvider ?? Sha3KeccackHashProvider.Instance;
        }

        public async Task<Account> GetAccountAsync(string address)
        {
            var fromInner = await _inner.GetAccountAsync(address).ConfigureAwait(false);
            if (fromInner != null) return fromInner;

            var stateRoot = _stateRootProvider();
            if (IsEmptyOrAllZero(stateRoot)) return null;

            var addrKey = _hashProvider.ComputeHash(AddressBytes(address));
            var trie = new PatriciaTrie(stateRoot, _nodeStore, _hashProvider);
            var rlp = trie.Get(addrKey);
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

            var slotKey = StateKeys.StorageSlotKey(slot);
            var storageTrie = new PatriciaTrie(account.StateRoot, _nodeStore, _hashProvider);
            var trieValue = storageTrie.Get(slotKey);
            if (trieValue == null || trieValue.Length == 0) return null;

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
        public async Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address)
        {
            var fromInner = await _inner.GetAllStorageAsync(address).ConfigureAwait(false);
            if (fromInner != null && fromInner.Count > 0) return fromInner;

            var account = await GetAccountAsync(address).ConfigureAwait(false);
            if (account?.StateRoot == null || IsEmptyOrAllZero(account.StateRoot)
                || ByteUtil.AreEqual(account.StateRoot, DefaultValues.EMPTY_TRIE_HASH))
                return fromInner ?? new Dictionary<byte[], byte[]>(ByteArrayComparer.Current);
            if (_trieStorage.Get(account.StateRoot) == null)
                return fromInner ?? new Dictionary<byte[], byte[]>(ByteArrayComparer.Current);

            var storageTrie = PatriciaTrie.LoadFromStorage(account.StateRoot, _nodeStore);
            var result = new Dictionary<byte[], byte[]>(ByteArrayComparer.Current);
            foreach (var entry in PatriciaRangeIterator.EnumerateRange(storageTrie.Root, _nodeStore, new byte[32]))
            {
                var raw = ((RLPItem)RLP.RLP.Decode(entry.Value))?.RLPData ?? entry.Value;
                result[entry.KeyBytes] = raw;
                if (_backfill)
                    await _inner.SaveStorageByKeccakAsync(address, entry.KeyBytes, raw).ConfigureAwait(false);
            }
            return result.Count > 0 ? result : (fromInner ?? result);
        }
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
