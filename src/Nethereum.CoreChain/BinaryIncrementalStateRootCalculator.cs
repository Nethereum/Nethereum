using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Binary;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Merkle.Binary.Keys;
using Nethereum.Merkle.Binary.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.CoreChain
{
    public class BinaryIncrementalStateRootCalculator
    {
        private static readonly byte[] EMPTY_ROOT = new byte[32];
        private static readonly byte[] EMPTY_CODE_HASH = Sha3Keccack.Current.CalculateHash(new byte[0]);

        private readonly IStateStore _stateStore;
        private readonly IHashProvider _hashProvider;
        private readonly BinaryTreeKeyDerivation _keyDerivation;
        private readonly IBinaryTrieStorage _trieStorage;

        private BinaryTrie _trie;
        private volatile bool _initialized;
        private volatile byte[] _cachedRoot;

        public BinaryIncrementalStateRootCalculator(
            IStateStore stateStore,
            IHashProvider hashProvider = null,
            IBinaryTrieStorage trieStorage = null)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _hashProvider = hashProvider ?? new Blake3HashProvider();
            _keyDerivation = new BinaryTreeKeyDerivation(_hashProvider);
            _trieStorage = trieStorage;
        }

        public async Task<byte[]> ComputeStateRootAsync()
        {
            if (!_initialized)
            {
                await InitializeFromFullStateAsync();
                _initialized = true;
                _cachedRoot = _trie.ComputeRoot();

                if (_trieStorage != null)
                    _trie.SaveToStorage(_trieStorage);

                await _stateStore.ClearDirtyTrackingAsync();
                return _cachedRoot;
            }

            var hasDirty = await UpdateFromDirtyAccountsAsync();
            await _stateStore.ClearDirtyTrackingAsync();

            if (!hasDirty && _cachedRoot != null)
                return _cachedRoot;

            _cachedRoot = _trie.ComputeRoot();

            if (_trieStorage != null)
                _trie.SaveToStorage(_trieStorage);

            return _cachedRoot;
        }

        public async Task<byte[]> ComputeFullStateRootAsync()
        {
            _trie = new BinaryTrie(_hashProvider);
            _initialized = false;

            await InitializeFromFullStateAsync();
            _initialized = true;
            _cachedRoot = _trie.ComputeRoot();

            if (_trieStorage != null)
                _trie.SaveToStorage(_trieStorage);

            return _cachedRoot;
        }

        public void Reset()
        {
            _trie = null;
            _initialized = false;
            _cachedRoot = null;
        }

        private async Task InitializeFromFullStateAsync()
        {
            _trie = new BinaryTrie(_hashProvider);

            var accounts = await _stateStore.GetAllAccountsAsync();
            if (accounts.Count == 0)
                return;

            foreach (var kvp in accounts)
            {
                await PutAccountInTrieAsync(kvp.Key, kvp.Value, useAllStorage: true);
            }
        }

        private async Task<bool> UpdateFromDirtyAccountsAsync()
        {
            var dirtyAddresses = await _stateStore.GetDirtyAccountAddressesAsync();
            if (dirtyAddresses.Count == 0)
                return false;

            foreach (var address in dirtyAddresses)
            {
                var account = await _stateStore.GetAccountAsync(address);
                if (account == null)
                {
                    DeleteAccountFromTrie(address);
                }
                else
                {
                    await PutAccountInTrieAsync(address, account, useAllStorage: false);
                }
            }

            return true;
        }

        private async Task PutAccountInTrieAsync(string address, Account account, bool useAllStorage)
        {
            var addressBytes = AddressUtil.Current.ConvertToValid20ByteAddress(address).HexToByteArray();

            PutBasicData(addressBytes, account);
            PutCodeHash(addressBytes, account);
            await PutCodeChunksAsync(addressBytes, account);

            if (useAllStorage)
                await PutAllStorageAsync(address, addressBytes);
            else
                await PutDirtyStorageAsync(address, addressBytes);
        }

        private void PutBasicData(byte[] addressBytes, Account account)
        {
            var nonce = (ulong)account.Nonce;
            var balance = account.Balance;
            var code = _stateStore.GetCodeAsync(account.CodeHash ?? EMPTY_CODE_HASH).GetAwaiter().GetResult();
            var codeSize = (uint)(code != null ? code.Length : 0);

            var leaf = BasicDataLeaf.Pack(0, codeSize, nonce, balance);
            var key = _keyDerivation.GetTreeKeyForBasicData(addressBytes);
            _trie.Put(key, leaf);
        }

        private void PutCodeHash(byte[] addressBytes, Account account)
        {
            var codeHash = account.CodeHash ?? EMPTY_CODE_HASH;
            var key = _keyDerivation.GetTreeKeyForCodeHash(addressBytes);
            _trie.Put(key, codeHash);
        }

        private async Task PutCodeChunksAsync(byte[] addressBytes, Account account)
        {
            var code = await _stateStore.GetCodeAsync(account.CodeHash ?? EMPTY_CODE_HASH);
            if (code == null || code.Length == 0)
                return;

            var chunks = CodeChunker.ChunkifyCode(code);
            for (int i = 0; i < chunks.Length; i++)
            {
                var key = _keyDerivation.GetTreeKeyForCodeChunk(addressBytes, (ulong)i);
                _trie.Put(key, chunks[i]);
            }
        }

        private async Task PutAllStorageAsync(string address, byte[] addressBytes)
        {
            var storage = await _stateStore.GetAllStorageAsync(address);
            if (storage == null || storage.Count == 0)
                return;

            foreach (var entry in storage)
            {
                PutStorageSlot(addressBytes, entry.Key, entry.Value);
            }
        }

        private async Task PutDirtyStorageAsync(string address, byte[] addressBytes)
        {
            var dirtySlots = await _stateStore.GetDirtyStorageSlotsAsync(address);
            if (dirtySlots.Count == 0)
                return;

            foreach (var slot in dirtySlots)
            {
                var value = await _stateStore.GetStorageAsync(address, slot);
                PutStorageSlot(addressBytes, slot, value);
            }
        }

        private void PutStorageSlot(byte[] addressBytes, BigInteger slot, byte[] value)
        {
            var storageKey = EvmUInt256BigIntegerExtensions.FromBigInteger(slot);
            var key = _keyDerivation.GetTreeKeyForStorageSlot(addressBytes, storageKey);

            if (value == null || value.Length == 0 || IsAllZero(value))
            {
                _trie.Delete(key);
                return;
            }

            _trie.Put(key, PadTo32(value));
        }

        private void DeleteAccountFromTrie(string address)
        {
            var addressBytes = AddressUtil.Current.ConvertToValid20ByteAddress(address).HexToByteArray();
            var basicKey = _keyDerivation.GetTreeKeyForBasicData(addressBytes);
            var codeHashKey = _keyDerivation.GetTreeKeyForCodeHash(addressBytes);
            _trie.Delete(basicKey);
            _trie.Delete(codeHashKey);
        }

        private static bool IsAllZero(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
                if (bytes[i] != 0) return false;
            return true;
        }

        private static byte[] PadTo32(byte[] value)
        {
            if (value.Length == 32) return value;
            if (value.Length > 32) return value;
            var padded = new byte[32];
            Array.Copy(value, 0, padded, 32 - value.Length, value.Length);
            return padded;
        }
    }
}
