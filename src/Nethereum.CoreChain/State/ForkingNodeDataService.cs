using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.CoreChain.State
{
    public class ForkingNodeDataService : INodeDataService
    {
        private readonly IStateStore _stateStore;
        private readonly IBlockStore _blockStore;
        private readonly RpcNodeDataService _remoteService;
        private readonly HashSet<string> _fetchedAccounts = new HashSet<string>();
        private readonly HashSet<string> _fetchedStorage = new HashSet<string>();
        private readonly object _fetchLock = new object();
        private readonly SemaphoreSlim _asyncLock = new SemaphoreSlim(1, 1);

        public ForkingNodeDataService(
            IStateStore stateStore,
            IBlockStore blockStore,
            IEthApiService remoteEthApi,
            BlockParameter forkBlock)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _blockStore = blockStore;
            _remoteService = new RpcNodeDataService(remoteEthApi, forkBlock);
        }

        public async Task<BigInteger> GetBalanceAsync(string address)
        {
            var normalizedAddress = NormalizeAddress(address);

            var account = await _stateStore.GetAccountAsync(address);
            if (account != null)
                return account.Balance;

            if (HasFetchedAccount(normalizedAddress))
                return BigInteger.Zero;

            await _asyncLock.WaitAsync();
            try
            {
                account = await _stateStore.GetAccountAsync(address);
                if (account != null)
                    return account.Balance;

                if (HasFetchedAccount(normalizedAddress))
                    return BigInteger.Zero;

                await FetchAndCacheAccountAsync(address, normalizedAddress);

                account = await _stateStore.GetAccountAsync(address);
                return account?.Balance ?? BigInteger.Zero;
            }
            finally
            {
                _asyncLock.Release();
            }
        }

        public Task<BigInteger> GetBalanceAsync(byte[] address)
        {
            return GetBalanceAsync(address.ToHex());
        }

        public async Task<byte[]> GetCodeAsync(string address)
        {
            var normalizedAddress = NormalizeAddress(address);

            var account = await _stateStore.GetAccountAsync(address);
            if (account?.CodeHash != null)
                return await _stateStore.GetCodeAsync(account.CodeHash);

            if (HasFetchedAccount(normalizedAddress))
                return null;

            await _asyncLock.WaitAsync();
            try
            {
                account = await _stateStore.GetAccountAsync(address);
                if (account?.CodeHash != null)
                    return await _stateStore.GetCodeAsync(account.CodeHash);

                if (HasFetchedAccount(normalizedAddress))
                    return null;

                await FetchAndCacheAccountAsync(address, normalizedAddress);

                account = await _stateStore.GetAccountAsync(address);
                if (account?.CodeHash != null)
                    return await _stateStore.GetCodeAsync(account.CodeHash);

                return null;
            }
            finally
            {
                _asyncLock.Release();
            }
        }

        public Task<byte[]> GetCodeAsync(byte[] address)
        {
            return GetCodeAsync(address.ToHex());
        }

        public async Task<byte[]> GetStorageAtAsync(string address, BigInteger position)
        {
            var value = await _stateStore.GetStorageAsync(address, position);
            if (value != null)
                return value;

            var storageKey = GetStorageKey(address, position);
            if (HasFetchedStorage(storageKey))
                return null;

            await _asyncLock.WaitAsync();
            try
            {
                value = await _stateStore.GetStorageAsync(address, position);
                if (value != null)
                    return value;

                if (HasFetchedStorage(storageKey))
                    return null;

                try
                {
                    value = await _remoteService.GetStorageAtAsync(address, position);
                    if (value != null && !IsZeroValue(value))
                    {
                        await _stateStore.SaveStorageAsync(address, position, value);
                    }
                }
                catch
                {
                    value = null;
                }

                MarkStorageFetched(storageKey);
                return value;
            }
            finally
            {
                _asyncLock.Release();
            }
        }

        public Task<byte[]> GetStorageAtAsync(byte[] address, BigInteger position)
        {
            return GetStorageAtAsync(address.ToHex(), position);
        }

        public async Task<BigInteger> GetTransactionCount(string address)
        {
            var normalizedAddress = NormalizeAddress(address);

            var account = await _stateStore.GetAccountAsync(address);
            if (account != null)
                return account.Nonce;

            if (HasFetchedAccount(normalizedAddress))
                return BigInteger.Zero;

            await _asyncLock.WaitAsync();
            try
            {
                account = await _stateStore.GetAccountAsync(address);
                if (account != null)
                    return account.Nonce;

                if (HasFetchedAccount(normalizedAddress))
                    return BigInteger.Zero;

                await FetchAndCacheAccountAsync(address, normalizedAddress);

                account = await _stateStore.GetAccountAsync(address);
                return account?.Nonce ?? BigInteger.Zero;
            }
            finally
            {
                _asyncLock.Release();
            }
        }

        public Task<BigInteger> GetTransactionCount(byte[] address)
        {
            return GetTransactionCount(address.ToHex());
        }

        public async Task<byte[]> GetBlockHashAsync(BigInteger blockNumber)
        {
            if (_blockStore != null)
            {
                var hash = await _blockStore.GetHashByNumberAsync(blockNumber);
                if (hash != null)
                    return hash;
            }

            try
            {
                return await _remoteService.GetBlockHashAsync(blockNumber);
            }
            catch
            {
                return null;
            }
        }

        private async Task FetchAndCacheAccountAsync(string address, string normalizedAddress)
        {
            try
            {
                var balanceTask = _remoteService.GetBalanceAsync(address);
                var nonceTask = _remoteService.GetTransactionCount(address);
                var codeTask = _remoteService.GetCodeAsync(address);

                await Task.WhenAll(balanceTask, nonceTask, codeTask);

                var balance = balanceTask.Result;
                var nonce = nonceTask.Result;
                var code = codeTask.Result;

                if (balance == BigInteger.Zero && nonce == BigInteger.Zero &&
                    (code == null || code.Length == 0))
                {
                    MarkAccountFetched(normalizedAddress);
                    return;
                }

                var account = new Account
                {
                    Balance = balance,
                    Nonce = nonce
                };

                if (code != null && code.Length > 0)
                {
                    var codeHash = new Sha3Keccack().CalculateHash(code);
                    await _stateStore.SaveCodeAsync(codeHash, code);
                    account.CodeHash = codeHash;
                }

                await _stateStore.SaveAccountAsync(address, account);
                MarkAccountFetched(normalizedAddress);
            }
            catch
            {
                MarkAccountFetched(normalizedAddress);
            }
        }

        private bool HasFetchedAccount(string normalizedAddress)
        {
            lock (_fetchLock)
            {
                return _fetchedAccounts.Contains(normalizedAddress);
            }
        }

        private void MarkAccountFetched(string normalizedAddress)
        {
            lock (_fetchLock)
            {
                _fetchedAccounts.Add(normalizedAddress);
            }
        }

        private bool HasFetchedStorage(string storageKey)
        {
            lock (_fetchLock)
            {
                return _fetchedStorage.Contains(storageKey);
            }
        }

        private void MarkStorageFetched(string storageKey)
        {
            lock (_fetchLock)
            {
                _fetchedStorage.Add(storageKey);
            }
        }

        private static string NormalizeAddress(string address)
        {
            if (string.IsNullOrEmpty(address))
                return string.Empty;
            return address.ToLowerInvariant().Replace("0x", "");
        }

        private static string GetStorageKey(string address, BigInteger position)
        {
            return $"{NormalizeAddress(address)}:{position}";
        }

        private static bool IsZeroValue(byte[] value)
        {
            if (value == null || value.Length == 0)
                return true;
            foreach (var b in value)
            {
                if (b != 0)
                    return false;
            }
            return true;
        }
    }
}
