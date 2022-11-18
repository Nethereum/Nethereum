using Nethereum.Util;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.EVM
{
    public class ExecutionStateService
    {
        
        public ExecutionStateService(INodeDataService nodeDataService)
        {
            this.NodeDataService = nodeDataService;
        }
        public Dictionary<string, AccountExecutionState> AccountsState { get; private set; } = new Dictionary<string, AccountExecutionState>();

        public INodeDataService NodeDataService { get; set; }

        public async Task<byte[]> GetFromStorageAsync(string address, BigInteger key)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            if (!accountState.StorageContainsKey(key))
            {
                var storageValue = await NodeDataService.GetStorageAtAsync(address, key);
                accountState.UpsertStorageValue(key, storageValue);
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

       
        public void SetNonce(string address, BigInteger nonce)
        {
            var accountState = CreateOrGetAccountExecutionState(address);
            accountState.Nonce = nonce;
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