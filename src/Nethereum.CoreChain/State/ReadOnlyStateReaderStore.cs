using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.State
{
    public sealed class ReadOnlyStateReaderStore : IStateStore
    {
        private readonly IStateReader _reader;
        private readonly ConcurrentDictionary<string, byte[]> _codeByHash = new(StringComparer.OrdinalIgnoreCase);
        private readonly Sha3Keccack _keccak = new();
        private int _nextSnapshotId;

        public ReadOnlyStateReaderStore(IStateReader reader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        public async Task<Account> GetAccountAsync(string address)
        {
            var balance = await _reader.GetBalanceAsync(address).ConfigureAwait(false);
            var nonce = await _reader.GetTransactionCountAsync(address).ConfigureAwait(false);
            var code = await _reader.GetCodeAsync(address).ConfigureAwait(false);

            byte[] codeHash;
            if (code == null || code.Length == 0)
            {
                codeHash = DefaultValues.EMPTY_DATA_HASH;
            }
            else
            {
                codeHash = _keccak.CalculateHash(code);
                _codeByHash[codeHash.ToHex()] = code;
            }

            // "zero-balance, zero-nonce, no code". Treat the all-zero case as
            // non-existent so the engine sees null and the EIP-158/161 empty-
            if (balance == EvmUInt256.Zero
                && nonce == EvmUInt256.Zero
                && IsEmptyCodeHash(codeHash))
            {
                return null;
            }

            return new Account
            {
                Balance = balance,
                Nonce = nonce,
                CodeHash = codeHash
            };
        }

        public Task<bool> AccountExistsAsync(string address)
        {
            return _reader.AccountExistsAsync(address);
        }

        public async Task<byte[]> GetStorageAsync(string address, BigInteger slot)
        {
            var slotKey = EvmUInt256BigIntegerExtensions.FromBigInteger(slot);
            return await _reader.GetStorageAtAsync(address, slotKey).ConfigureAwait(false);
        }

        public Task<byte[]> GetCodeAsync(byte[] codeHash)
        {
            if (codeHash == null) return Task.FromResult<byte[]>(null);
            if (IsEmptyCodeHash(codeHash)) return Task.FromResult<byte[]>(null);
            var hex = codeHash.ToHex();
            return Task.FromResult(_codeByHash.TryGetValue(hex, out var code) ? code : null);
        }


        public Task SaveAccountAsync(string address, Account account)
            => throw new NotSupportedException("ReadOnlyStateReaderStore is read-only — wrap in ReadOnlyStateStoreWrapper to absorb writes.");

        public Task DeleteAccountAsync(string address)
            => throw new NotSupportedException("ReadOnlyStateReaderStore is read-only — wrap in ReadOnlyStateStoreWrapper to absorb writes.");

        public Task SaveStorageAsync(string address, BigInteger slot, byte[] value)
            => throw new NotSupportedException("ReadOnlyStateReaderStore is read-only — wrap in ReadOnlyStateStoreWrapper to absorb writes.");

        public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value)
            => throw new NotSupportedException("ReadOnlyStateReaderStore is read-only — wrap in ReadOnlyStateStoreWrapper to absorb writes.");

        public Task ClearStorageAsync(string address)
            => throw new NotSupportedException("ReadOnlyStateReaderStore is read-only — wrap in ReadOnlyStateStoreWrapper to absorb writes.");

        public Task SaveCodeAsync(byte[] codeHash, byte[] code)
            => throw new NotSupportedException("ReadOnlyStateReaderStore is read-only — wrap in ReadOnlyStateStoreWrapper to absorb writes.");


        public Task<Dictionary<string, Account>> GetAllAccountsAsync()
            => throw new NotSupportedException("Cannot enumerate full state via IStateReader.");

        public IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync()
            => throw new NotSupportedException("Cannot enumerate full state via IStateReader.");

        public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address)
            => throw new NotSupportedException("Cannot enumerate full storage via IStateReader.");


        public Task<Storage.IStateSnapshot> CreateSnapshotAsync()
        {
            var id = System.Threading.Interlocked.Increment(ref _nextSnapshotId);
            return Task.FromResult<Storage.IStateSnapshot>(new NoOpSnapshot(id));
        }

        public Task CommitSnapshotAsync(Storage.IStateSnapshot snapshot) => Task.CompletedTask;

        public Task RevertSnapshotAsync(Storage.IStateSnapshot snapshot) => Task.CompletedTask;


        public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync()
            => Task.FromResult<IReadOnlyCollection<string>>(Array.Empty<string>());

        public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address)
            => Task.FromResult<IReadOnlyCollection<BigInteger>>(Array.Empty<BigInteger>());

        public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync()
            => Task.FromResult<IReadOnlyCollection<string>>(Array.Empty<string>());

        public Task ClearDirtyTrackingAsync() => Task.CompletedTask;

        private static bool IsEmptyCodeHash(byte[] codeHash)
        {
            if (codeHash == null || codeHash.Length != DefaultValues.EMPTY_DATA_HASH.Length)
                return false;
            for (int i = 0; i < codeHash.Length; i++)
            {
                if (codeHash[i] != DefaultValues.EMPTY_DATA_HASH[i])
                    return false;
            }
            return true;
        }

        private sealed class NoOpSnapshot : Storage.IStateSnapshot
        {
            public NoOpSnapshot(int id) { SnapshotId = id; }
            public int SnapshotId { get; }
            public void SetAccount(string address, Account account) { }
            public void SetStorage(string address, BigInteger slot, byte[] value) { }
            public void SetCode(byte[] codeHash, byte[] code) { }
            public void DeleteAccount(string address) { }
            public void ClearStorage(string address) { }
            public void Dispose() { }
        }
    }
}
