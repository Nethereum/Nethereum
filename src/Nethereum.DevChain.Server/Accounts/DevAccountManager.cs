using System.Numerics;
using Nethereum.Wallet.Bip32;
using Nethereum.DevChain.Server.Configuration;

namespace Nethereum.DevChain.Server.Accounts
{
    public class DevAccountManager
    {
        private readonly List<DevAccount> _accounts = new();
        private readonly HashSet<string> _impersonatedAccounts = new(StringComparer.OrdinalIgnoreCase);
        private readonly MinimalHDWallet _wallet;
        private readonly BigInteger _chainId;

        public IReadOnlyList<DevAccount> Accounts => _accounts;
        public IReadOnlySet<string> ImpersonatedAccounts => _impersonatedAccounts;

        public DevAccountManager(DevChainServerConfig config)
        {
            _wallet = new MinimalHDWallet(config.Mnemonic);
            _chainId = config.ChainId;

            var initialBalance = config.GetAccountBalance();

            for (int i = 0; i < config.AccountCount; i++)
            {
                var key = _wallet.GetEthereumKey(i);
                _accounts.Add(new DevAccount(i, key, _chainId, initialBalance));
            }
        }

        public DevAccount? GetAccount(int index)
        {
            if (index < 0 || index >= _accounts.Count)
                return null;
            return _accounts[index];
        }

        public DevAccount? GetAccountByAddress(string address)
        {
            return _accounts.FirstOrDefault(a =>
                string.Equals(a.Address, address, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsDevAccount(string address)
        {
            return _accounts.Any(a =>
                string.Equals(a.Address, address, StringComparison.OrdinalIgnoreCase));
        }

        public bool CanSign(string address)
        {
            return IsDevAccount(address) || _impersonatedAccounts.Contains(address);
        }

        public void ImpersonateAccount(string address)
        {
            _impersonatedAccounts.Add(address);
        }

        public void StopImpersonatingAccount(string address)
        {
            _impersonatedAccounts.Remove(address);
        }

        public bool IsImpersonated(string address)
        {
            return _impersonatedAccounts.Contains(address);
        }

        public string[] GetAllAddresses()
        {
            return _accounts.Select(a => a.Address).ToArray();
        }
    }
}
