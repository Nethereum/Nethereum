using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.DevP2P.SyncNode
{
    /// <summary>
    /// Fixture-emission-only IStateStore decorator. Captures the FIRST-READ
    /// VALUE for each account, code, and storage slot accessed during block
    /// execution. After the block runs, these captured values ARE the
    /// canonical pre-state for those addresses (because subsequent reads in
    /// the same block see the modified values, and the first read happens
    /// before any write to that account).
    ///
    /// This is intentionally SEPARATE from <see cref="Nethereum.EVM.AccessListTracker"/>:
    /// the AccessListTracker captures addresses + slots for eth_createAccessList
    /// (EIP-2930) without VALUES. The fixture pre-state needs the actual
    /// VALUES (balance/nonce/code/storage) at the moment before the block ran.
    ///
    /// Used by <see cref="MainnetBlockFixtureEmitter"/> to produce
    /// regression-cell fixtures with rich enough pre-state that
    /// <c>InMemoryStateStore</c>-based test replays produce the same
    /// per-account post-state as canonical mainnet — including sub-CALL
    /// targets, contracts referenced by the called contract, etc.
    /// </summary>
    internal sealed class FixturePreStateRecorder : IStateStore, IHistoricalStateProvider
    {
        private readonly IStateStore _inner;

        public ConcurrentDictionary<string, Account> FirstReadAccount { get; } = new();
        public ConcurrentDictionary<byte[], byte[]> FirstReadCode { get; } = new();
        public ConcurrentDictionary<(string addr, BigInteger slot), byte[]> FirstReadStorage { get; } = new();

        /// <summary>
        /// When false, reads pass through with zero capture overhead and
        /// no dictionary growth. Wrap the state store permanently and
        /// toggle this only around the target fixture blocks.
        /// </summary>
        public bool IsRecording { get; set; }

        public FixturePreStateRecorder(IStateStore inner) { _inner = inner; }

        public IReadOnlyDictionary<string, Account> CapturedAccounts => FirstReadAccount;

        public void BeginRecording()
        {
            FirstReadAccount.Clear();
            FirstReadCode.Clear();
            FirstReadStorage.Clear();
            IsRecording = true;
        }

        public void EndRecording() => IsRecording = false;

        public async Task<Account> GetAccountAsync(string address)
        {
            var account = await _inner.GetAccountAsync(address);
            if (IsRecording)
            {
                FirstReadAccount.TryAdd(address.ToLowerInvariant(), CloneAccount(account));
            }
            return account;
        }

        public Task SaveAccountAsync(string address, Account account)
            => _inner.SaveAccountAsync(address, account);

        public Task<bool> AccountExistsAsync(string address) => _inner.AccountExistsAsync(address);

        public Task DeleteAccountAsync(string address) => _inner.DeleteAccountAsync(address);

        public Task<Dictionary<string, Account>> GetAllAccountsAsync() => _inner.GetAllAccountsAsync();
        public System.Collections.Generic.IAsyncEnumerable<KeyValuePair<string, Account>> StreamAccountsAsync() => _inner.StreamAccountsAsync();

        public async Task<byte[]> GetStorageAsync(string address, BigInteger slot)
        {
            var value = await _inner.GetStorageAsync(address, slot);
            if (IsRecording)
            {
                var key = (address.ToLowerInvariant(), slot);
                if (!FirstReadStorage.ContainsKey(key))
                {
                    FirstReadStorage.TryAdd(key, CloneBytes(value));
                }
            }
            return value;
        }

        public Task SaveStorageAsync(string address, BigInteger slot, byte[] value)
            => _inner.SaveStorageAsync(address, slot, value);

        public Task SaveStorageByKeccakAsync(string address, byte[] slotKeccak, byte[] value)
            => _inner.SaveStorageByKeccakAsync(address, slotKeccak, value);

        public Task<Dictionary<byte[], byte[]>> GetAllStorageAsync(string address)
            => _inner.GetAllStorageAsync(address);

        public Task ClearStorageAsync(string address) => _inner.ClearStorageAsync(address);

        public async Task<byte[]> GetCodeAsync(byte[] codeHash)
        {
            var code = await _inner.GetCodeAsync(codeHash);
            if (IsRecording && codeHash != null && !FirstReadCode.ContainsKey(codeHash))
            {
                FirstReadCode.TryAdd(CloneBytes(codeHash), CloneBytes(code));
            }
            return code;
        }

        public Task SaveCodeAsync(byte[] codeHash, byte[] code) => _inner.SaveCodeAsync(codeHash, code);

        public Task<IStateSnapshot> CreateSnapshotAsync() => _inner.CreateSnapshotAsync();
        public Task CommitSnapshotAsync(IStateSnapshot snapshot) => _inner.CommitSnapshotAsync(snapshot);
        public Task RevertSnapshotAsync(IStateSnapshot snapshot) => _inner.RevertSnapshotAsync(snapshot);
        public Task<IReadOnlyCollection<string>> GetDirtyAccountAddressesAsync() => _inner.GetDirtyAccountAddressesAsync();
        public Task<IReadOnlyCollection<BigInteger>> GetDirtyStorageSlotsAsync(string address) => _inner.GetDirtyStorageSlotsAsync(address);
        public Task<IReadOnlyCollection<string>> GetStorageClearedAddressesAsync() => _inner.GetStorageClearedAddressesAsync();
        public Task ClearDirtyTrackingAsync() => _inner.ClearDirtyTrackingAsync();

        public void SetCurrentBlockNumber(BigInteger blockNumber)
        {
            if (_inner is IHistoricalStateProvider provider)
                provider.SetCurrentBlockNumber(blockNumber);
        }

        public Task ClearCurrentBlockNumberAsync()
        {
            if (_inner is IHistoricalStateProvider provider)
                return provider.ClearCurrentBlockNumberAsync();
            return Task.CompletedTask;
        }

        public Task<Account> GetAccountAtBlockAsync(string address, BigInteger blockNumber)
        {
            if (_inner is IHistoricalStateProvider provider)
                return provider.GetAccountAtBlockAsync(address, blockNumber);
            return Task.FromResult<Account>(null);
        }

        public Task<byte[]> GetStorageAtBlockAsync(string address, BigInteger slot, BigInteger blockNumber)
        {
            if (_inner is IHistoricalStateProvider provider)
                return provider.GetStorageAtBlockAsync(address, slot, blockNumber);
            return Task.FromResult<byte[]>(null);
        }

        private static byte[] CloneBytes(byte[] src) => src == null ? null : (byte[])src.Clone();

        private static Account CloneAccount(Account src)
        {
            if (src == null) return null;
            return new Account
            {
                Nonce = src.Nonce,
                Balance = src.Balance,
                CodeHash = CloneBytes(src.CodeHash)
            };
        }
    }
}
