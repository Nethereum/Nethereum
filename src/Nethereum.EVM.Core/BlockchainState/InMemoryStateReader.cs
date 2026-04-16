using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.BlockchainState
{
    public class InMemoryStateReader : IStateReader
    {
        private readonly Dictionary<string, AccountState> _accounts;

        /// <summary>
        /// When true, any read of an account/slot not present in the supplied
        /// data throws <see cref="MissingWitnessDataException"/> instead of
        /// silently returning zero. Use for witness-driven execution where a
        /// miss indicates a recorder bug — silent zeros diverge state roots.
        /// </summary>
        public bool Strict { get; set; }

        public InMemoryStateReader(Dictionary<string, AccountState> accounts)
        {
            _accounts = accounts ?? new Dictionary<string, AccountState>();
        }

        private void MissingAccount(string address, string what)
        {
            if (Strict) throw new MissingWitnessDataException(
                $"{what} for address '{address}' is not in witness (Strict mode).");
        }
        private void MissingSlot(string address, EvmUInt256 position)
        {
            if (Strict) throw new MissingWitnessDataException(
                $"Storage slot '{position}' for address '{address}' is not in witness (Strict mode).");
        }

        public AccountState GetAccountState(string address) => GetAccount(address);
        public IReadOnlyDictionary<string, AccountState> Accounts => _accounts;

        private AccountState GetAccount(string address)
        {
            if (address != null && _accounts.TryGetValue(address.ToLower(), out var account))
                return account;
            return null;
        }

        public void CommitChanges(ExecutionStateService executionState)
        {
            foreach (var kvp in executionState.AccountsState)
            {
                var address = kvp.Key.ToLower();
                if (!_accounts.TryGetValue(address, out var account))
                {
                    account = new AccountState();
                    _accounts[address] = account;
                }
                // Ensure InitialChainBalance is loaded before computing total
                // (accounts touched by FinalizeTransaction may not have loaded it)
                if (kvp.Value.Balance.InitialChainBalance == null)
                    kvp.Value.Balance.SetInitialChainBalance(account.Balance);
                account.Balance = kvp.Value.Balance.GetTotalBalance();
                if (kvp.Value.Nonce.HasValue)
                    account.Nonce = kvp.Value.Nonce.Value;
                if (kvp.Value.Code != null)
                    account.Code = kvp.Value.Code;
                foreach (var storageKvp in kvp.Value.Storage)
                    account.Storage[storageKvp.Key] = storageKvp.Value;
            }
        }

#if EVM_SYNC
        public EvmUInt256 GetBalance(byte[] address) => GetBalance(address.ToHex(true));
        public EvmUInt256 GetBalance(string address)
        {
            var account = GetAccount(address);
            if (account == null) { MissingAccount(address, "Balance"); return EvmUInt256.Zero; }
            return account.Balance;
        }
        public byte[] GetCode(byte[] address) => GetCode(address.ToHex(true));
        public byte[] GetCode(string address)
        {
            var account = GetAccount(address);
            if (account == null) { MissingAccount(address, "Code"); return new byte[0]; }
            return account.Code ?? new byte[0];
        }
        public byte[] GetStorageAt(byte[] address, EvmUInt256 position) => GetStorageAt(address.ToHex(true), position);
        public byte[] GetStorageAt(string address, EvmUInt256 position)
        {
            var account = GetAccount(address);
            if (account == null) { MissingAccount(address, "Storage"); return new byte[32]; }
            if (account.Storage.TryGetValue(position, out var val)) return val;
            MissingSlot(address, position);
            return new byte[32];
        }
        public EvmUInt256 GetTransactionCount(byte[] address) => GetTransactionCount(address.ToHex(true));
        public EvmUInt256 GetTransactionCount(string address)
        {
            var account = GetAccount(address);
            if (account == null) { MissingAccount(address, "Nonce"); return EvmUInt256.Zero; }
            return account.Nonce;
        }
#else
        public Task<EvmUInt256> GetBalanceAsync(byte[] address) => GetBalanceAsync(address.ToHex(true));
        public Task<EvmUInt256> GetBalanceAsync(string address)
        {
            var account = GetAccount(address);
            if (account == null) { MissingAccount(address, "Balance"); return Task.FromResult(EvmUInt256.Zero); }
            return Task.FromResult(account.Balance);
        }
        public Task<byte[]> GetCodeAsync(byte[] address) => GetCodeAsync(address.ToHex(true));
        public Task<byte[]> GetCodeAsync(string address)
        {
            var account = GetAccount(address);
            if (account == null) { MissingAccount(address, "Code"); return Task.FromResult(new byte[0]); }
            return Task.FromResult(account.Code ?? new byte[0]);
        }
        public Task<byte[]> GetStorageAtAsync(byte[] address, EvmUInt256 position) => GetStorageAtAsync(address.ToHex(true), position);
        public Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 position)
        {
            var account = GetAccount(address);
            if (account == null) { MissingAccount(address, "Storage"); return Task.FromResult(new byte[32]); }
            if (account.Storage.TryGetValue(position, out var val)) return Task.FromResult(val);
            MissingSlot(address, position);
            return Task.FromResult(new byte[32]);
        }
        public Task<EvmUInt256> GetTransactionCountAsync(byte[] address) => GetTransactionCountAsync(address.ToHex(true));
        public Task<EvmUInt256> GetTransactionCountAsync(string address)
        {
            var account = GetAccount(address);
            if (account == null) { MissingAccount(address, "Nonce"); return Task.FromResult(EvmUInt256.Zero); }
            return Task.FromResult(account.Nonce);
        }
#endif
    }
}
