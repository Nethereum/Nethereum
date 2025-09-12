#nullable enable

using Nethereum.Signer;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Accounts;
using Nethereum.Wallet.Bip32;
using Nethereum.Wallet.WalletAccounts;

namespace Nethereum.Wallet
{
    public class CoreWalletAccountService : ICoreWalletAccountService
    {
        private readonly WalletVault _walletVault;

        public CoreWalletAccountService(WalletVault walletVault)
        {
            _walletVault = walletVault;
        }

        public Task<IWalletAccount> CreatePrivateKeyAccountAsync(string privateKey, string? label = null)
        {
            var key = new EthECKey(privateKey);
            var address = key.GetPublicAddress();
            var account = new PrivateKeyWalletAccount(address, label ?? address, privateKey);
            return Task.FromResult<IWalletAccount>(account);
        }

        public Task<IWalletAccount> CreateMnemonicAccountAsync(string mnemonic, string? passphrase = null, string? label = null)
        {
            var hdWallet = new MinimalHDWallet(mnemonic, passphrase ?? string.Empty);
            var address = hdWallet.GetEthereumAddress(0);
            var index = 0;
            var mnemonicInfo = new MnemonicInfo(label ?? address, mnemonic, passphrase);
            _walletVault.AddMnemonic(mnemonicInfo);
            var account = new MnemonicWalletAccount(address, label ?? address, index, mnemonicInfo.Id, hdWallet);
            return Task.FromResult<IWalletAccount>(account);
        }

        public Task<IWalletAccount> CreateViewOnlyAccountAsync(string address, string? label = null)
        {
            var account = new ViewOnlyWalletAccount(address, label ?? address);
            return Task.FromResult<IWalletAccount>(account);
        }

        public Task<IWalletAccount> CreateSmartContractAccountAsync(string address, string? label = null)
        {
            var account = new SmartContractWalletAccount(address, label ?? address);
            return Task.FromResult<IWalletAccount>(account);
        }
    }
}