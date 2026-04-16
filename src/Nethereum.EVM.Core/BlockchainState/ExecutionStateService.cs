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
        private readonly Stack<IStateSnapshot> _snapshots = new Stack<IStateSnapshot>();
        private int _nextSnapshotId = 0;
        private readonly HashSet<string> _warmAddresses = new HashSet<string>();

        public ExecutionStateService(IStateReader stateReader)
        {
            StateReader = stateReader;
        }

        public Dictionary<string, AccountExecutionState> AccountsState { get; private set; } = new Dictionary<string, AccountExecutionState>();
        public Dictionary<string, Dictionary<EvmUInt256, byte[]>> TransientStorage { get; private set; } = new Dictionary<string, Dictionary<EvmUInt256, byte[]>>(StringComparer.OrdinalIgnoreCase);

        public IStateReader StateReader { get; set; }

        public IStateReader NodeDataService => StateReader;


        public int TakeSnapshot()
        {
            var snapshot = new StateSnapshot(_nextSnapshotId, AccountsState, _warmAddresses, TransientStorage);
            _snapshots.Push(snapshot);

            return _nextSnapshotId++;
        }

        public void RevertToSnapshot(int snapshotId)
        {
            while (_snapshots.Count > 0)
            {
                var snapshot = _snapshots.Peek();
                if (snapshot.SnapshotId == snapshotId)
                {
                    RestoreFromSnapshot(snapshot);
                    return;
                }
                _snapshots.Pop();
            }
#if EVM_SYNC
            return;
#else
            throw new System.InvalidOperationException($"Snapshot {snapshotId} not found");
#endif
        }

        public void CommitSnapshot(int snapshotId)
        {
            var tempStack = new Stack<IStateSnapshot>();
            while (_snapshots.Count > 0)
            {
                var snapshot = _snapshots.Pop();
                if (snapshot.SnapshotId == snapshotId)
                {
                    while (tempStack.Count > 0)
                    {
                        _snapshots.Push(tempStack.Pop());
                    }
                    return;
                }
                tempStack.Push(snapshot);
            }
            while (tempStack.Count > 0)
            {
                _snapshots.Push(tempStack.Pop());
            }
        }

        public void DiscardSnapshot(int snapshotId)
        {
            CommitSnapshot(snapshotId);
        }

        private void RestoreFromSnapshot(IStateSnapshot snapshot)
        {
            _warmAddresses.Clear();
            foreach (var addr in snapshot.WarmAddresses)
            {
                _warmAddresses.Add(addr);
            }

            var addressesToRemove = new List<string>();
            foreach (var addr in AccountsState.Keys)
            {
                if (!snapshot.AccountSnapshots.ContainsKey(addr))
                    addressesToRemove.Add(addr);
            }

            foreach (var addr in addressesToRemove)
            {
                AccountsState.Remove(addr);
            }

            foreach (var kvp in snapshot.AccountSnapshots)
            {
                RestoreAccountState(kvp.Key, kvp.Value);
            }

            TransientStorage.Clear();
            if (snapshot.TransientStorage != null)
            {
                foreach (var kvp in snapshot.TransientStorage)
                {
                    var copy = new Dictionary<EvmUInt256, byte[]>();
                    foreach (var inner in kvp.Value)
                    {
                        copy[inner.Key] = (byte[])inner.Value?.Clone();
                    }
                    TransientStorage[kvp.Key] = copy;
                }
            }
        }

        private void RestoreAccountState(string address, AccountStateSnapshot snapshot)
        {
            if (!AccountsState.ContainsKey(address))
            {
                AccountsState[address] = new AccountExecutionState { Address = snapshot.Address };
            }

            var accountState = AccountsState[address];
            accountState.Storage.Clear();
            foreach (var kvp in snapshot.Storage)
            {
                accountState.Storage[kvp.Key] = (byte[])kvp.Value?.Clone();
            }
            accountState.Balance.SetExecutionBalance(snapshot.ExecutionBalance);
            if (snapshot.InitialChainBalance.HasValue)
                accountState.Balance.SetInitialChainBalance(snapshot.InitialChainBalance.Value);
            else
                accountState.Balance.ClearInitialChainBalance();
            accountState.Nonce = snapshot.Nonce;
            accountState.Code = (byte[])snapshot.Code?.Clone();
            accountState.IsNewContract = snapshot.IsNewContract;

            accountState.WarmStorageKeys.Clear();
            if (snapshot.WarmStorageKeys != null)
            {
                foreach (var key in snapshot.WarmStorageKeys)
                {
                    accountState.WarmStorageKeys.Add(key);
                }
            }
        }

#if EVM_SYNC
        public byte[] GetFromStorage(string address, EvmUInt256 key)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            if (!accountState.StorageContainsKey(key))
            {
                var storageValue = StateReader.GetStorageAt(address, key);
                accountState.TrackAndWriteStorage(key, storageValue);
            }

            return accountState.GetStorageValue(key);
        }

        public byte[] GetCode(string address)
        {
            var normalizedAddress = AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLower();

            var accountState = CreateOrGetAccountExecutionState(normalizedAddress);

            if (accountState.Code == null)
            {
                accountState.Code = StateReader.GetCode(normalizedAddress);
            }
            return accountState.Code;
        }

        public EvmUInt256 GetNonce(string address)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            if (accountState.Nonce == null)
            {
                accountState.Nonce = StateReader.GetTransactionCount(address);
            }
            return accountState.Nonce.Value;
        }

        public AccountExecutionState LoadBalanceNonceAndCodeFromStorage(string address)
        {
            GetCode(address);
            GetNonce(address);
            GetTotalBalance(address);
            return CreateOrGetAccountExecutionState(address);
        }

        public EvmUInt256 GetTotalBalance(string address)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            if (!ContainsInitialChainBalanceForAddress(address))
            {
                var balanceChain = StateReader.GetBalance(address);
                accountState.Balance.SetInitialChainBalance(balanceChain);
            }
            var balance = accountState.Balance.GetTotalBalance();
            return balance;
        }

        public bool AccountExists(string address)
        {
            var balance = GetTotalBalance(address);
            if (!balance.IsZero) return true;

            var nonce = GetNonce(address);
            if (nonce > 0) return true;

            var code = GetCode(address);
            if (code != null && code.Length > 0) return true;

            return false;
        }
#else
        public async Task<byte[]> GetFromStorageAsync(string address, EvmUInt256 key)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            if (!accountState.StorageContainsKey(key))
            {
                var storageValue = await StateReader.GetStorageAtAsync(address, key);
                accountState.TrackAndWriteStorage(key, storageValue);
            }

            return accountState.GetStorageValue(key);
        }

        public async Task<byte[]> GetCodeAsync(string address)
        {
            var normalizedAddress = AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLower();

            var accountState = CreateOrGetAccountExecutionState(normalizedAddress);

            if (accountState.Code == null)
            {
                accountState.Code = await StateReader.GetCodeAsync(normalizedAddress);
            }
            return accountState.Code;
        }

        public async Task<EvmUInt256> GetNonceAsync(string address)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            if (accountState.Nonce == null)
            {
                accountState.Nonce = await StateReader.GetTransactionCountAsync(address);
            }
            return accountState.Nonce.Value;
        }

        public async Task<AccountExecutionState> LoadBalanceNonceAndCodeFromStorageAsync(string address)
        {
            await GetCodeAsync(address);
            await GetNonceAsync(address);
            await GetTotalBalanceAsync(address);
            return CreateOrGetAccountExecutionState(address);
        }

        public async Task<EvmUInt256> GetTotalBalanceAsync(string address)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            if (!ContainsInitialChainBalanceForAddress(address))
            {
                var balanceChain = await StateReader.GetBalanceAsync(address);
                accountState.Balance.SetInitialChainBalance(balanceChain);
            }
            var balance = accountState.Balance.GetTotalBalance();
            return balance;
        }

        public async Task<bool> AccountExistsAsync(string address)
        {
            var balance = await GetTotalBalanceAsync(address);
            if (!balance.IsZero) return true;

            var nonce = await GetNonceAsync(address);
            if (nonce > 0) return true;

            var code = await GetCodeAsync(address);
            if (code != null && code.Length > 0) return true;

            return false;
        }
#endif

        public void SaveCode(string address, byte[] code)
        {
            address = address.ToLower();
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.Code = code;
        }

        public void SetNonce(string address, EvmUInt256 nonce)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.Nonce = nonce;
        }

        public void PrepareNewContractAccount(string address, ulong initialNonce = 1)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.ClearStorageForNewContract();
            accountState.Nonce = initialNonce;
            accountState.Code = null;
            accountState.IsNewContract = true;
        }

        public bool AddressIsWarm(string address)
        {
            foreach (var w in _warmAddresses)
            {
                if (w.IsTheSameAddress(address))
                    return true;
            }
            return false;
        }

        public void MarkAddressAsWarm(string address)
        {
            if (!AddressIsWarm(address))
            {
                var normalized = AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLower();
                _warmAddresses.Add(normalized);
            }
            CreateOrGetAccountExecutionState(address);
        }

        public void MarkPrecompilesAsWarm(Execution.Precompiles.PrecompileRegistry registry)
        {
            System.Diagnostics.Debug.Assert(registry != null,
                "PrecompileRegistry is null on the HardforkConfig — precompiles won't be warmed. " +
                "Reference Nethereum.EVM.Precompiles or pass a precompile-wired HardforkConfig.");
            if (registry == null) return;
            foreach (var addressInt in registry.GetAddresses())
                MarkAddressAsWarm(AddressUtil.Current.ConvertToValid20ByteAddress(addressInt.ToString("x")));
        }

        /// <summary>
        /// Checks if an account is empty (zero balance, zero nonce, no code) without
        /// creating an AccountExecutionState entry. Uses in-memory state if available,
        /// falls back to the state reader.
        /// </summary>
#if EVM_SYNC
        public bool IsAccountEmpty(string address)
#else
        public async Task<bool> IsAccountEmptyAsync(string address)
#endif
        {
            address = AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLower();

            // Inspect the in-memory execution state first; any modification during
            // this tx overrides the underlying state reader.
            AccountExecutionState acct = null;
            if (AccountsState.ContainsKey(address))
            {
                acct = AccountsState[address];
                if (acct.Balance.GetTotalBalance() > 0) return false;
                if (acct.Nonce.HasValue && acct.Nonce.Value > 0) return false;
                if (acct.Code != null && acct.Code.Length > 0) return false;
            }

            // Fall through to the state reader for any field not yet loaded into
            // AccountsState. Returning true without checking would misclassify a
            // pre-existing contract as empty when its code hasn't been lazy-loaded yet.
#if EVM_SYNC
            if (acct == null || !acct.Balance.InitialChainBalance.HasValue)
            {
                var balance = StateReader.GetBalance(address);
                if (balance > 0) return false;
            }
            if (acct == null || !acct.Nonce.HasValue)
            {
                var nonce = StateReader.GetTransactionCount(address);
                if (nonce > 0) return false;
            }
            if (acct == null || acct.Code == null)
            {
                var code = StateReader.GetCode(address);
                if (code != null && code.Length > 0) return false;
            }
#else
            if (acct == null || !acct.Balance.InitialChainBalance.HasValue)
            {
                var balance = await StateReader.GetBalanceAsync(address);
                if (balance > 0) return false;
            }
            if (acct == null || !acct.Nonce.HasValue)
            {
                var nonce = await StateReader.GetTransactionCountAsync(address);
                if (nonce > 0) return false;
            }
            if (acct == null || acct.Code == null)
            {
                var code = await StateReader.GetCodeAsync(address);
                if (code != null && code.Length > 0) return false;
            }
#endif
            return true;
        }

        private static bool IsPrecompileAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return false;
            var hex = address.StartsWith("0x") || address.StartsWith("0X") ? address.Substring(2) : address;
            var compact = hex.TrimStart('0');
            if (compact.Length == 0) return false;
            if (int.TryParse(compact, System.Globalization.NumberStyles.HexNumber, null, out int addressNum))
            {
                return addressNum >= 1 && addressNum <= 17;
            }
            return false;
        }

        public AccountExecutionState CreateOrGetAccountExecutionState(string address)
        {
            address = AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLower();
            if (!AccountsState.ContainsKey(address))
            {
                AccountsState.Add(address,
                    new AccountExecutionState() { Address = address });
            }
            return AccountsState[address];
        }

        public void SaveToStorage(string address, EvmUInt256 key, byte[] storageValue)
        {
            address = address.ToLower();
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.UpsertStorageValue(key, storageValue);
        }

        public void SetPreStateStorage(string address, EvmUInt256 key, byte[] storageValue)
        {
            address = address.ToLower();
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.SetPreStateStorage(key, storageValue);
        }

        public bool ContainsInitialChainBalanceForAddress(string address)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            return accountState.Balance.InitialChainBalance != null;
        }

        public void SetInitialChainBalance(string address, EvmUInt256 value)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.Balance.SetInitialChainBalance(value);
        }

        public void CreditBalance(string address, EvmUInt256 value)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.Balance.CreditExecutionBalance(value);
        }

        public void DebitBalance(string address, EvmUInt256 value)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.Balance.DebitExecutionBalance(value);
        }

        public void DeleteAccount(string address)
        {
            address = AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLower();
            if (AccountsState.ContainsKey(address))
            {
                AccountsState.Remove(address);
            }
        }
    }
}
