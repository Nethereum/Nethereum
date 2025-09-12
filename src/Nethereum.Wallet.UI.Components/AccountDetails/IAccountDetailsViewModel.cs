using Nethereum.Wallet.WalletAccounts;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.AccountDetails
{
    public interface IAccountDetailsViewModel
    {
        string AccountType { get; }
        bool CanHandle(IWalletAccount account);
        Task InitializeAsync(IWalletAccount account);
        IWalletAccount? Account { get; }
        bool IsLoading { get; }
        string ErrorMessage { get; }
        string SuccessMessage { get; }
        void ClearMessages();
    }
}