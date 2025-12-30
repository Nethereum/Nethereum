using System.Collections.Generic;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using Nethereum.Wallet.UI.Components.Utils;

namespace Nethereum.Wallet.UI.Components.Holdings
{
    public partial class HoldingsTokenItemViewModel : ObservableObject
    {
        [ObservableProperty] private string _symbol;
        [ObservableProperty] private string _name;
        [ObservableProperty] private string _logoUri;
        [ObservableProperty] private decimal _totalBalance;
        [ObservableProperty] private decimal? _totalValue;
        [ObservableProperty] private decimal? _price;
        [ObservableProperty] private int _decimals;
        [ObservableProperty] private bool _isExpanded;

        public string CurrencySymbol { get; set; } = "$";
        public int ChainCount => ChainBalances?.Count ?? 0;
        public List<ChainBalanceItemViewModel> ChainBalances { get; set; } = new();

        public string FormattedTotalBalance => CurrencyFormatter.FormatBalance(TotalBalance);

        public string FormattedTotalValue => CurrencyFormatter.FormatValue(TotalValue, CurrencySymbol);

        public string FormattedPrice => CurrencyFormatter.FormatPrice(Price, CurrencySymbol);

        public TokenInfoViewModel Token => new TokenInfoViewModel { Symbol = Symbol, Name = Name, LogoUri = LogoUri };
    }

    public class TokenInfoViewModel
    {
        public string Symbol { get; set; } = "";
        public string Name { get; set; } = "";
        public string LogoUri { get; set; } = "";
        public decimal? TotalValue { get; set; }
        public int Decimals { get; set; }
        public List<ChainBalanceItemViewModel> ChainBalances { get; set; } = new();
    }

    public class ChainBalanceItemViewModel
    {
        public ChainBalanceInfo ChainBalance { get; set; } = new();
        public int Decimals { get; set; }
        public string CurrencySymbol { get; set; } = "$";
        public long ChainId { get => ChainBalance.ChainId; set => ChainBalance.ChainId = value; }
        public string ChainName { get => ChainBalance.ChainName; set => ChainBalance.ChainName = value; }
        public decimal Balance { get; set; }
        public decimal? Value { get; set; }
        public int AccountCount => AccountBalances?.Count ?? 0;
        public bool IsExpanded { get; set; }
        public List<AccountBalanceItemViewModel> AccountBalances { get; set; } = new();

        public string FormattedBalance => CurrencyFormatter.FormatBalance(Balance);
        public string FormattedValue => CurrencyFormatter.FormatValue(Value, CurrencySymbol);

        public ChainBalanceItemViewModel() { }

        public ChainBalanceItemViewModel(object chainBalance, int decimals, string currencySymbol)
        {
            if (chainBalance is ChainBalanceInfo cbi)
                ChainBalance = cbi;
            Decimals = decimals;
            CurrencySymbol = currencySymbol;
        }
    }

    public class ChainBalanceInfo
    {
        public long ChainId { get; set; }
        public string ChainName { get; set; } = "";
        public BigInteger Balance { get; set; }
        public decimal? Value { get; set; }
    }

    public class AccountBalanceItemViewModel
    {
        public string Address { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Balance { get; set; }
        public decimal? Value { get; set; }
        public string CurrencySymbol { get; set; } = "$";
        public AccountBalanceInfo AccountBalance { get; set; } = new();
        public string AccountAddress { get => Address; set => Address = value; }

        public string FormattedAddress
        {
            get
            {
                if (string.IsNullOrEmpty(Address) || Address.Length < 10) return Address ?? "";
                return $"{Address.Substring(0, 6)}...{Address.Substring(Address.Length - 4)}";
            }
        }

        public string FormattedBalance => CurrencyFormatter.FormatBalance(Balance);
        public string FormattedValue => CurrencyFormatter.FormatValue(Value, CurrencySymbol);
    }

    public class AccountBalanceInfo
    {
        public string Address { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Balance { get; set; }
        public decimal? Value { get; set; }
        public string AccountAddress { get => Address; set => Address = value; }
    }
}
