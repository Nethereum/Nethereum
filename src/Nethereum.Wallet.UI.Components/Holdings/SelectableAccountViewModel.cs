using CommunityToolkit.Mvvm.ComponentModel;

namespace Nethereum.Wallet.UI.Components.Holdings
{
    public partial class SelectableAccountViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _forceRescan;

        public string Address { get; }
        public string Name { get; }
        public string AccountType { get; }
        public IWalletAccount Account { get; }

        public SelectableAccountViewModel(IWalletAccount account)
        {
            Account = account;
            Address = account.Address ?? "";
            Name = !string.IsNullOrEmpty(account.Name) ? account.Name
                 : !string.IsNullOrEmpty(account.Label) ? account.Label
                 : FormatAddress(account.Address);
            AccountType = account.Type ?? "";
        }

        public string FormattedAddress => FormatAddress(Address);

        private static string FormatAddress(string address)
        {
            if (string.IsNullOrEmpty(address) || address.Length < 10)
                return address ?? "";
            return $"{address.Substring(0, 6)}...{address.Substring(address.Length - 4)}";
        }
    }
}
