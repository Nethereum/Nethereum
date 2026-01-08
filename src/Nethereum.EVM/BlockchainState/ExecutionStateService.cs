using Nethereum.Util;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.EVM.BlockchainState
{
    public class ExecutionStateService
    {
        private readonly Stack<IStateSnapshot> _snapshots = new Stack<IStateSnapshot>();
        private int _nextSnapshotId = 0;

        public ExecutionStateService(INodeDataService nodeDataService)
        {
            NodeDataService = nodeDataService;
        }

        public Dictionary<string, AccountExecutionState> AccountsState { get; private set; } = new Dictionary<string, AccountExecutionState>();

        public INodeDataService NodeDataService { get; set; }

        public int TakeSnapshot()
        {
            var snapshot = new StateSnapshot(_nextSnapshotId, AccountsState);
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
            throw new System.InvalidOperationException($"Snapshot {snapshotId} not found");
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
            var addressesToRemove = AccountsState.Keys
                .Where(addr => !snapshot.AccountSnapshots.ContainsKey(addr))
                .ToList();

            foreach (var addr in addressesToRemove)
            {
                AccountsState.Remove(addr);
            }

            foreach (var kvp in snapshot.AccountSnapshots)
            {
                RestoreAccountState(kvp.Key, kvp.Value);
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
                accountState.Storage[kvp.Key] = kvp.Value?.ToArray();
            }
            accountState.Balance.SetExecutionBalance(snapshot.ExecutionBalance);
            accountState.Nonce = snapshot.Nonce;
            accountState.Code = snapshot.Code?.ToArray();
        }



        public async Task<byte[]> GetFromStorageAsync(string address, BigInteger key)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            if (!accountState.StorageContainsKey(key))
            {
                var storageValue = await NodeDataService.GetStorageAtAsync(address, key);
                accountState.TrackAndWriteStorage(key, storageValue);
            }

            return accountState.GetStorageValue(key);
        }

        public async Task<byte[]> GetCodeAsync(string address)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            if (accountState.Code == null)
            {
                accountState.Code = await NodeDataService.GetCodeAsync(address);
            }
            return accountState.Code;
        }

        public void SaveCode(string address, byte[] code)
        {
            address = address.ToLower();
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.Code = code;
        }

        public async Task<BigInteger> GetNonceAsync(string address)
        {   //contracts start at 1 if zero?
            var accountState = CreateOrGetAccountExecutionState(address);
            if (accountState.Nonce == null)
            {
                accountState.Nonce = await NodeDataService.GetTransactionCount(address);
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


        public void SetNonce(string address, BigInteger nonce)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.Nonce = nonce;
        }

        public bool AddressIsWarm(string address)
        {
            address = AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLower();
            return AccountsState.ContainsKey(address);
        }

        public void MarkAddressAsWarm(string address)
        {
            CreateOrGetAccountExecutionState(address);
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

        public void SaveToStorage(string address, BigInteger key, byte[] storageValue)
        {
            address = address.ToLower();
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.UpsertStorageValue(key, storageValue);
        }

        public bool ContainsInitialChainBalanceForAddress(string address)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            return accountState.Balance.InitialChainBalance != null;
        }

        public async Task<BigInteger> GetTotalBalanceAsync(string address)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            if (!ContainsInitialChainBalanceForAddress(address))
            {
                var balanceChain = await NodeDataService.GetBalanceAsync(address);
                accountState.Balance.SetInitialChainBalance(balanceChain);
            }
            var balance = accountState.Balance.GetTotalBalance();
            return balance;
        }

        public void SetInitialChainBalance(string address, BigInteger value)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.Balance.SetInitialChainBalance(value);
        }

        public void UpsertInternalBalance(string address, BigInteger value)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.Balance.UpdateExecutionBalance(value);
        }
    }
}