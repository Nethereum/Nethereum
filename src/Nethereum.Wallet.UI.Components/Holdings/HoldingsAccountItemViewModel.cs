using CommunityToolkit.Mvvm.ComponentModel;
using Nethereum.Wallet.UI.Components.Utils;

namespace Nethereum.Wallet.UI.Components.Holdings
{
    public partial class HoldingsAccountItemViewModel : ObservableObject
    {
        [ObservableProperty] private string _address;
        [ObservableProperty] private string _name;
        [ObservableProperty] private decimal _totalValue;
        [ObservableProperty] private bool _isIncluded = true;
        [ObservableProperty] private bool _isExpanded;

        public string FormattedAddress
        {
            get
            {
                if (string.IsNullOrEmpty(Address) || Address.Length < 10) return Address ?? "";
                return $"{Address.Substring(0, 6)}...{Address.Substring(Address.Length - 4)}";
            }
        }

        public string FormattedTotalValue => CurrencyFormatter.FormatValue(TotalValue, "$");
    }
}
