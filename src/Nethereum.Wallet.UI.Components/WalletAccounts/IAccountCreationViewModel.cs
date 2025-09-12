using Nethereum.Wallet;

namespace Nethereum.Wallet.UI.Components.WalletAccounts
{
    public interface IAccountCreationViewModel
    {
        string DisplayName { get; }
        string Description { get; }
        string Icon { get; }
        int SortOrder { get; }
        bool IsVisible { get; }
        bool CanCreateAccount { get; }
        IWalletAccount CreateAccount(WalletVault vault);
        void Reset();
    }
}