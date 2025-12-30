using CommunityToolkit.Mvvm.ComponentModel;
using Nethereum.Wallet.UI.Components.Utils;

namespace Nethereum.Wallet.UI.Components.Holdings
{
    public partial class HoldingsNetworkItemViewModel : ObservableObject
    {
        [ObservableProperty] private long _chainId;
        [ObservableProperty] private string _name;
        [ObservableProperty] private string _nativeSymbol;
        [ObservableProperty] private decimal _totalValue;
        [ObservableProperty] private int _tokenCount;
        [ObservableProperty] private bool _isIncluded = true;
        [ObservableProperty] private bool _isExpanded;
        [ObservableProperty] private bool _hasError;
        [ObservableProperty] private string _errorMessage;
        [ObservableProperty] private bool _isScanning;
        [ObservableProperty] private string _scanProgressText = "";

        public string FormattedTotalValue => CurrencyFormatter.FormatValue(TotalValue, "$");
    }
}
