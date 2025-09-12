#nullable enable

using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Accounts; // Assuming IAccount is here or a similar core abstraction

namespace Nethereum.Wallet
{
    public interface ICoreWalletAccountService
    {
        Task<IWalletAccount> CreatePrivateKeyAccountAsync(string privateKey, string? label = null);
        Task<IWalletAccount> CreateMnemonicAccountAsync(string mnemonic, string? passphrase = null, string? label = null);
        Task<IWalletAccount> CreateViewOnlyAccountAsync(string address, string? label = null);
        Task<IWalletAccount> CreateSmartContractAccountAsync(string address, string? label = null);
    }
}