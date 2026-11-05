using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.Numerics;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.BlockchainState
{
    public class ExecutionStateService
    {
        private readonly StateJournal _journal;
        private const int WarmAddressCapacity = 8;
        private readonly HashSet<EvmAddress> _warmAddresses = NewWarmAddressSet();
        private Execution.Precompiles.PrecompileRegistry _warmPrecompiles;
        private readonly HashSet<EvmAddress> _selfDestructedAddresses = new HashSet<EvmAddress>();
        private readonly Dictionary<EvmAddress, byte[]> _chainCodeReadWithoutResolving = new Dictionary<EvmAddress, byte[]>();

#if !EVM_SYNC
        public System.Threading.CancellationToken CancellationToken { get; set; }
#endif

        public bool HasSelfDestructed(string address)
        {
            if (string.IsNullOrEmpty(address)) return false;
            return HasSelfDestructed(EvmAddress.FromHex(address));
        }

        public bool HasSelfDestructed(EvmAddress address)
        {
            return _selfDestructedAddresses.Contains(address);
        }

        public void MarkSelfDestructed(string address)
        {
            if (string.IsNullOrEmpty(address)) return;
            MarkSelfDestructed(EvmAddress.FromHex(address));
        }

        public void MarkSelfDestructed(EvmAddress address)
        {
            if (_selfDestructedAddresses.Add(address)) _journal.SelfDestructAdded(address);
        }

        private readonly HashSet<EvmAddress> _txGloballyTouched = new HashSet<EvmAddress>();

        public void MarkTxGloballyTouched(string address)
        {
            if (string.IsNullOrEmpty(address)) return;
            MarkTxGloballyTouched(EvmAddress.FromHex(address));
        }

        public void MarkTxGloballyTouched(EvmAddress address)
        {
            _txGloballyTouched.Add(address);
        }

        public IEnumerable<EvmAddress> TxGloballyTouchedAddresses => _txGloballyTouched;

        public ExecutionStateService(IStateReader stateReader)
        {
            StateReader = stateReader;
            _journal = new StateJournal(AccountsState, _warmAddresses, _selfDestructedAddresses, TransientStorage);
        }

        public Dictionary<EvmAddress, AccountExecutionState> AccountsState { get; private set; } = new Dictionary<EvmAddress, AccountExecutionState>();
        public Dictionary<EvmAddress, Dictionary<EvmUInt256, byte[]>> TransientStorage { get; private set; } = new Dictionary<EvmAddress, Dictionary<EvmUInt256, byte[]>>();

        public IStateReader StateReader { get; set; }

        public IStateReader NodeDataService => StateReader;

        public bool TouchPersistsOnRevert { get; set; } = false;

        public IStateAccessRecorder AccessRecorder { get; set; }

        internal void NotifyAccountRead(EvmAddress address)
        {
            var recorder = AccessRecorder;
            if (recorder == null) return;
            if (recorder is IAddressAccessRecorder byAddress) byAddress.RecordAccountRead(address);
            else recorder.RecordAccountRead(address.ToHexLower());
        }

        internal void NotifyStorageRead(EvmAddress address, EvmUInt256 key)
        {
            var recorder = AccessRecorder;
            if (recorder == null) return;
            if (recorder is IAddressAccessRecorder byAddress) byAddress.RecordStorageRead(address, key);
            else recorder.RecordStorageRead(address.ToHexLower(), key);
        }


        public int TakeSnapshot()
        {
            return _journal.Take();
        }

        private static void RequireTakenSnapshot(int snapshotId, string operation)
        {
            if (snapshotId < 0)
                throw new System.InvalidOperationException(
                    operation + " was called with snapshot id " + snapshotId +
                    ", which is TransactionExecutionContext.NotTaken. The caller built a context and " +
                    "never took the snapshot it is now trying to unwind to.");
        }

        public void RevertToSnapshot(int snapshotId)
        {
            RequireTakenSnapshot(snapshotId, nameof(RevertToSnapshot));
            if (_journal.Revert(snapshotId)) return;
#if !EVM_SYNC
            throw new System.InvalidOperationException($"Snapshot {snapshotId} not found");
#endif
        }

        public void CommitSnapshot(int snapshotId)
        {
            RequireTakenSnapshot(snapshotId, nameof(CommitSnapshot));
            _journal.Commit(snapshotId);
        }

        public void DiscardSnapshot(int snapshotId)
        {
            CommitSnapshot(snapshotId);
        }

#if EVM_SYNC
        public byte[] GetFromStorage(string address, EvmUInt256 key) => GetFromStorage(EvmAddress.FromHex(address), key);

        public byte[] GetFromStorage(EvmAddress address, EvmUInt256 key)
        {
            NotifyStorageRead(address, key);
            return GetFromStorageWithoutRecordingAccess(address, key);
        }

        /// <summary>
        /// Reads a storage slot exactly like <see cref="GetFromStorage(EvmAddress,EvmUInt256)"/> —
        /// same account materialisation, same <c>IsStorageReset</c> handling,
        /// same caching — but WITHOUT notifying <see cref="AccessRecorder"/>.
        ///
        /// <para><b>This is not a general-purpose alternative to
        /// <see cref="GetFromStorage(EvmAddress,EvmUInt256)"/>.</b> It exists for exactly one shape of
        /// caller: a client-side mechanism that resolves an opcode's result
        /// from state the reference implementation never reads for that
        /// opcode at all. EIP-2935 serving BLOCKHASH from the history
        /// predeploy (<see cref="Execution.Opcodes.Executors.Rules.Eip2935BlockHashRule"/>)
        /// is the motivating — and, so far, only — case: EELS resolves
        /// BLOCKHASH from <c>block_env.block_hashes</c> and performs no state
        /// access whatsoever, so routing the read through the recording
        /// accessor would report an EIP-7928 access-list entry the reference
        /// never produces.</para>
        ///
        /// <para>A suppression mode on <see cref="AccessRecorder"/> itself was
        /// considered and rejected: <see cref="IStateAccessRecorder"/>'s
        /// contract is "notified whenever execution reads", and a flag toggled
        /// around one call would stay set past it if anything between the
        /// toggle and the reset throws — which the EVM does routinely. A
        /// distinctly named sibling method has no state to leak; calling it is
        /// the only way to opt out, so a stack unwind can never leave the
        /// engine in a half-suppressed state. Call this ONLY where the
        /// reference genuinely performs no state access for the read in
        /// question — using it for an ordinary opcode's state access silently
        /// starves the block access list of an entry the reference expects.</para>
        /// </summary>
        public byte[] GetFromStorageWithoutRecordingAccess(string address, EvmUInt256 key) => GetFromStorageWithoutRecordingAccess(EvmAddress.FromHex(address), key);

        public byte[] GetFromStorageWithoutRecordingAccess(EvmAddress address, EvmUInt256 key)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            if (!accountState.StorageContainsKey(key))
            {
                var storageValue = accountState.IsStorageReset
                    ? new byte[32]
                    : StateReader.GetStorageAt(address.ToByteArray(), key);
                accountState.TrackAndWriteStorage(key, storageValue);
            }

            return accountState.GetStorageValue(key);
        }

        public byte[] GetCode(string address) => GetCode(EvmAddress.FromHex(address));

        public byte[] GetCode(EvmAddress address)
        {
            NotifyAccountRead(address);

            var accountState = CreateOrGetAccountExecutionState(address);

            if (!accountState.IsCodeResolved)
            {
                accountState.RecordChainCode(StateReader.GetCode(address.ToByteArray()));
            }
            return accountState.Code;
        }

        public byte[] GetCodeReadOnly(string address) => GetCodeReadOnly(EvmAddress.FromHex(address));

        public byte[] GetCodeReadOnly(EvmAddress address)
        {
            NotifyAccountRead(address);
            if (AccountsState.TryGetValue(address, out var existing) && existing.IsCodeResolved)
                return existing.Code;
            if (_chainCodeReadWithoutResolving.TryGetValue(address, out var read))
                return read;
            var chainCode = StateReader.GetCode(address.ToByteArray());
            _chainCodeReadWithoutResolving[address] = chainCode;
            return chainCode;
        }

        public EvmUInt256 GetNonce(string address) => GetNonce(EvmAddress.FromHex(address));

        public EvmUInt256 GetNonce(EvmAddress address)
        {
            NotifyAccountRead(address);
            var accountState = CreateOrGetAccountExecutionState(address);
            if (accountState.Nonce == null)
            {
                accountState.Nonce = StateReader.GetTransactionCount(address.ToByteArray());
            }
            return accountState.Nonce.Value;
        }

        public AccountExecutionState LoadBalanceNonceAndCodeFromStorage(string address) => LoadBalanceNonceAndCodeFromStorage(EvmAddress.FromHex(address));

        public AccountExecutionState LoadBalanceNonceAndCodeFromStorage(EvmAddress address)
        {
            GetCode(address);
            GetNonce(address);
            GetTotalBalance(address);
            return CreateOrGetAccountExecutionState(address);
        }

        public EvmUInt256 GetTotalBalance(string address) => GetTotalBalance(EvmAddress.FromHex(address));

        public EvmUInt256 GetTotalBalance(EvmAddress address)
        {
            NotifyAccountRead(address);
            var accountState = CreateOrGetAccountExecutionState(address);
            if (accountState.Balance.InitialChainBalance == null)
            {
                var balanceChain = StateReader.GetBalance(address.ToByteArray());
                accountState.Balance.SetInitialChainBalance(balanceChain);
            }
            var balance = accountState.Balance.GetTotalBalance();
            return balance;
        }

        public bool AccountExists(string address) => AccountExists(EvmAddress.FromHex(address));

        public bool AccountExists(EvmAddress address)
        {
            NotifyAccountRead(address);
            var balance = GetTotalBalance(address);
            if (!balance.IsZero) return true;

            var nonce = GetNonce(address);
            if (nonce > 0) return true;

            var code = GetCode(address);
            if (code != null && code.Length > 0) return true;

            return false;
        }
#else
        public Task<byte[]> GetFromStorageAsync(string address, EvmUInt256 key) => GetFromStorageAsync(EvmAddress.FromHex(address), key);

        public Task<byte[]> GetFromStorageAsync(EvmAddress address, EvmUInt256 key)
        {
            NotifyStorageRead(address, key);
            return GetFromStorageWithoutRecordingAccessAsync(address, key);
        }

        public Task<byte[]> GetFromStorageWithoutRecordingAccessAsync(string address, EvmUInt256 key) => GetFromStorageWithoutRecordingAccessAsync(EvmAddress.FromHex(address), key);

        public async Task<byte[]> GetFromStorageWithoutRecordingAccessAsync(EvmAddress address, EvmUInt256 key)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            if (!accountState.StorageContainsKey(key))
            {
                var storageValue = accountState.IsStorageReset
                    ? new byte[32]
                    : await StateReader.GetStorageAtAsync(address.ToByteArray(), key);
                accountState.TrackAndWriteStorage(key, storageValue);
            }

            return accountState.GetStorageValue(key);
        }

        public Task<byte[]> GetCodeAsync(string address) => GetCodeAsync(EvmAddress.FromHex(address));

        public async Task<byte[]> GetCodeAsync(EvmAddress address)
        {
            NotifyAccountRead(address);

            var accountState = CreateOrGetAccountExecutionState(address);

            if (!accountState.IsCodeResolved)
            {
                accountState.RecordChainCode(await StateReader.GetCodeAsync(address.ToByteArray()));
            }
            return accountState.Code;
        }

        public Task<byte[]> GetCodeReadOnlyAsync(string address) => GetCodeReadOnlyAsync(EvmAddress.FromHex(address));

        public async Task<byte[]> GetCodeReadOnlyAsync(EvmAddress address)
        {
            NotifyAccountRead(address);

            if (AccountsState.TryGetValue(address, out var existing) && existing.IsCodeResolved)
                return existing.Code;

            if (_chainCodeReadWithoutResolving.TryGetValue(address, out var read))
                return read;

            var chainCode = await StateReader.GetCodeAsync(address.ToByteArray());
            _chainCodeReadWithoutResolving[address] = chainCode;
            return chainCode;
        }

        public Task<EvmUInt256> GetNonceAsync(string address) => GetNonceAsync(EvmAddress.FromHex(address));

        public async Task<EvmUInt256> GetNonceAsync(EvmAddress address)
        {
            NotifyAccountRead(address);
            var accountState = CreateOrGetAccountExecutionState(address);
            if (accountState.Nonce == null)
            {
                accountState.Nonce = await StateReader.GetTransactionCountAsync(address.ToByteArray());
            }
            return accountState.Nonce.Value;
        }

        public Task<AccountExecutionState> LoadBalanceNonceAndCodeFromStorageAsync(string address) => LoadBalanceNonceAndCodeFromStorageAsync(EvmAddress.FromHex(address));

        public Task<AccountExecutionState> LoadBalanceNonceAndCodeFromStorageAsync(EvmAddress address)
        {
            var loaded = FindLoadedAccount(address);
            return loaded != null ? Task.FromResult(loaded) : ReadBalanceNonceAndCodeFromStorageAsync(address);
        }

        private async Task<AccountExecutionState> ReadBalanceNonceAndCodeFromStorageAsync(EvmAddress address)
        {
            await GetCodeAsync(address);
            await GetNonceAsync(address);
            await GetTotalBalanceAsync(address);
            return CreateOrGetAccountExecutionState(address);
        }

        private AccountExecutionState FindLoadedAccount(EvmAddress address)
        {
            if (!AccountsState.TryGetValue(address, out var account)) return null;
            if (!account.IsCodeResolved || !account.Nonce.HasValue || !account.Balance.InitialChainBalance.HasValue) return null;
            NotifyAccountRead(address);
            return account;
        }

        public Task<EvmUInt256> GetTotalBalanceAsync(string address) => GetTotalBalanceAsync(EvmAddress.FromHex(address));

        public async Task<EvmUInt256> GetTotalBalanceAsync(EvmAddress address)
        {
            NotifyAccountRead(address);
            var accountState = CreateOrGetAccountExecutionState(address);
            if (accountState.Balance.InitialChainBalance == null)
            {
                var balanceChain = await StateReader.GetBalanceAsync(address.ToByteArray());
                accountState.Balance.SetInitialChainBalance(balanceChain);
            }
            var balance = accountState.Balance.GetTotalBalance();
            return balance;
        }

        public Task<bool> AccountExistsAsync(string address) => AccountExistsAsync(EvmAddress.FromHex(address));

        public async Task<bool> AccountExistsAsync(EvmAddress address)
        {
            NotifyAccountRead(address);
            var balance = await GetTotalBalanceAsync(address);
            if (!balance.IsZero) return true;

            var nonce = await GetNonceAsync(address);
            if (nonce > 0) return true;

            var code = await GetCodeAsync(address);
            if (code != null && code.Length > 0) return true;

            return false;
        }
#endif

        public void SaveCode(string address, byte[] code) => SaveCode(EvmAddress.FromHex(address), code);

        public void SaveCode(EvmAddress address, byte[] code)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.IsTouched = true;
            accountState.Code = code;
        }

        public void SetNonce(string address, EvmUInt256 nonce) => SetNonce(EvmAddress.FromHex(address), nonce);

        public void SetNonce(EvmAddress address, EvmUInt256 nonce)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.IsTouched = true;
            accountState.Nonce = nonce;
        }

        public void PrepareNewContractAccount(string address, ulong initialNonce = 1) => PrepareNewContractAccount(EvmAddress.FromHex(address), initialNonce);

        public void PrepareNewContractAccount(EvmAddress address, ulong initialNonce = 1)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.ClearStorageForNewContract();
            accountState.Nonce = initialNonce;
            accountState.Code = null;
            accountState.IsNewContract = true;
        }

        public bool AddressIsWarm(string address) => AddressIsWarm(EvmAddress.FromHex(address));

        public bool AddressIsWarm(EvmAddress address)
        {
            return _warmAddresses.Contains(address) || (_warmPrecompiles != null && _warmPrecompiles.Handles(address));
        }

        public void MarkAddressAsWarm(string address) => MarkAddressAsWarm(EvmAddress.FromHex(address));

        public void MarkAddressAsWarm(EvmAddress address)
        {
            if (_warmAddresses.Add(address)) _journal.WarmAddressAdded(address);
        }

        public void MarkPrecompilesAsWarm(Execution.Precompiles.PrecompileRegistry registry)
        {
            if (registry != null) _warmPrecompiles = registry;
        }

        private static HashSet<EvmAddress> NewWarmAddressSet()
        {
#if NETCOREAPP2_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            return new HashSet<EvmAddress>(WarmAddressCapacity);
#else
            return new HashSet<EvmAddress>();
#endif
        }

#if EVM_SYNC
        public bool IsAccountEmpty(string address) => IsAccountEmpty(EvmAddress.FromHex(address));

        public bool IsAccountEmpty(EvmAddress address)
        {
            AccountExecutionState acct = null;
            if (AccountsState.TryGetValue(address, out var existing))
            {
                acct = existing;
                if (!acct.IsEmptyPerEip161()) return false;
            }

            if (acct == null || !acct.Balance.InitialChainBalance.HasValue)
            {
                var balance = StateReader.GetBalance(address.ToByteArray());
                if (balance > 0) return false;
            }
            if (acct == null || !acct.Nonce.HasValue)
            {
                var nonce = StateReader.GetTransactionCount(address.ToByteArray());
                if (nonce > 0) return false;
            }
            if (acct == null || !acct.IsCodeResolved)
            {
                var code = StateReader.GetCode(address.ToByteArray());
                if (code != null && code.Length > 0) return false;
            }
            return true;
        }
#else
        public Task<bool> IsAccountEmptyAsync(string address) => IsAccountEmptyAsync(EvmAddress.FromHex(address));

        public async Task<bool> IsAccountEmptyAsync(EvmAddress address)
        {
            AccountExecutionState acct = null;
            if (AccountsState.TryGetValue(address, out var existing))
            {
                acct = existing;
                if (!acct.IsEmptyPerEip161()) return false;
            }

            if (acct == null || !acct.Balance.InitialChainBalance.HasValue)
            {
                var balance = await StateReader.GetBalanceAsync(address.ToByteArray());
                if (balance > 0) return false;
            }
            if (acct == null || !acct.Nonce.HasValue)
            {
                var nonce = await StateReader.GetTransactionCountAsync(address.ToByteArray());
                if (nonce > 0) return false;
            }
            if (acct == null || !acct.IsCodeResolved)
            {
                var code = await StateReader.GetCodeAsync(address.ToByteArray());
                if (code != null && code.Length > 0) return false;
            }
            return true;
        }
#endif

        public AccountExecutionState CreateOrGetAccountExecutionState(string address) => CreateOrGetAccountExecutionState(EvmAddress.FromHex(address));

        public AccountExecutionState CreateOrGetAccountExecutionState(EvmAddress address)
        {
            if (!AccountsState.TryGetValue(address, out var accountState))
            {
                accountState = new AccountExecutionState() { Address = address.ToHexLower(), Journal = _journal };
                AccountsState.Add(address, accountState);
                _journal.AccountAdded(address);
            }
            return accountState;
        }

        public void SaveToStorage(string address, EvmUInt256 key, byte[] storageValue) => SaveToStorage(EvmAddress.FromHex(address), key, storageValue);

        public void SaveToStorage(EvmAddress address, EvmUInt256 key, byte[] storageValue)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.UpsertStorageValue(key, storageValue);
        }

        public void SetTransientStorage(EvmAddress address, EvmUInt256 key, byte[] value)
        {
            if (!TransientStorage.TryGetValue(address, out var addressStorage))
            {
                addressStorage = new Dictionary<EvmUInt256, byte[]>();
                TransientStorage[address] = addressStorage;
                _journal.TransientOuterAdded(address);
            }
            if (_journal.Recording)
            {
                var hadPrior = addressStorage.TryGetValue(key, out var prior);
                _journal.TransientSet(address, key, hadPrior, prior);
            }
            addressStorage[key] = value;
        }

        public void SetPreStateStorage(string address, EvmUInt256 key, byte[] storageValue) => SetPreStateStorage(EvmAddress.FromHex(address), key, storageValue);

        public void SetPreStateStorage(EvmAddress address, EvmUInt256 key, byte[] storageValue)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.SetPreStateStorage(key, storageValue);
        }

#if EVM_SYNC
        public bool AccountHasStorage(string address) => AccountHasStorage(EvmAddress.FromHex(address));

        public bool AccountHasStorage(EvmAddress address)
        {
            if (OverlayHoldsANonZeroSlot(address)) return true;
            if (OverlayShadowsChainStorage(address)) return false;
            if (StateReader is IAccountStorageAddressReader byAddress) return byAddress.AccountHasStorage(address);
            return StateReader is IAccountStorageReader storageReader
                && storageReader.AccountHasStorage(address.ToHexLower());
        }
#else
        public Task<bool> AccountHasStorageAsync(string address) => AccountHasStorageAsync(EvmAddress.FromHex(address));

        public async Task<bool> AccountHasStorageAsync(EvmAddress address)
        {
            if (OverlayHoldsANonZeroSlot(address)) return true;
            if (OverlayShadowsChainStorage(address)) return false;
            if (StateReader is IAccountStorageAddressReader byAddress) return await byAddress.AccountHasStorageAsync(address);
            return StateReader is IAccountStorageReader storageReader
                && await storageReader.AccountHasStorageAsync(address.ToHexLower());
        }
#endif

        private bool OverlayHoldsANonZeroSlot(EvmAddress address)
        {
            return TryGetAccountExecutionState(address, out var accountState)
                && accountState.Storage != null
                && AccountStorageValues.AnyNonZero(accountState.Storage.Values);
        }

        private bool OverlayShadowsChainStorage(EvmAddress address)
        {
            return TryGetAccountExecutionState(address, out var accountState)
                && accountState.IsStorageReset;
        }

        private bool TryGetAccountExecutionState(EvmAddress address, out AccountExecutionState accountState)
        {
            return AccountsState.TryGetValue(address, out accountState);
        }

        public bool ContainsInitialChainBalanceForAddress(string address) => ContainsInitialChainBalanceForAddress(EvmAddress.FromHex(address));

        public bool ContainsInitialChainBalanceForAddress(EvmAddress address)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            return accountState.Balance.InitialChainBalance != null;
        }

        public void SetInitialChainBalance(string address, EvmUInt256 value) => SetInitialChainBalance(EvmAddress.FromHex(address), value);

        public void SetInitialChainBalance(EvmAddress address, EvmUInt256 value)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.Balance.SetInitialChainBalance(value);
        }

        public void CreditBalance(string address, EvmUInt256 value) => CreditBalance(EvmAddress.FromHex(address), value);

        public void CreditBalance(EvmAddress address, EvmUInt256 value)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.Balance.CreditExecutionBalance(value);
        }

        public void DebitBalance(string address, EvmUInt256 value) => DebitBalance(EvmAddress.FromHex(address), value);

        public void DebitBalance(EvmAddress address, EvmUInt256 value)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.Balance.DebitExecutionBalance(value);
        }

        public void ClearAccountPreservingBalance(string address) => ClearAccountPreservingBalance(EvmAddress.FromHex(address));

        public void ClearAccountPreservingBalance(EvmAddress address)
        {
            if (!AccountsState.TryGetValue(address, out var account)) return;

            account.Nonce = EvmUInt256.Zero;
            account.Code = new byte[0];
            account.ResetStorageForOverride();
        }

        public void DeleteAccount(string address) => DeleteAccount(EvmAddress.FromHex(address));

        public void DeleteAccount(EvmAddress address)
        {
            if (!AccountsState.TryGetValue(address, out var account)) return;

            account.Balance.DebitExecutionBalance(account.Balance.GetTotalBalance());
            account.IsRemoved = true;
        }
    }
}
