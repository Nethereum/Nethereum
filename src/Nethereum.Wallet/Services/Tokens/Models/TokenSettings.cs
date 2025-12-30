using System.Collections.Generic;

namespace Nethereum.Wallet.Services.Tokens.Models
{
    public class TokenSettings
    {
        public string Currency { get; set; } = "usd";
        public string CurrencySymbol { get; set; } = "$";
        public int RefreshIntervalSeconds { get; set; } = 300;
        public bool AutoRefreshPrices { get; set; } = true;
        public List<string> ExcludedAccounts { get; set; } = new List<string>();
        public List<long> ExcludedChainIds { get; set; } = new List<long>();
    }

    public static class SupportedCurrencies
    {
        public static readonly Dictionary<string, string> Currencies = new Dictionary<string, string>
        {
            { "usd", "$" },
            { "eur", "€" },
            { "gbp", "£" },
            { "jpy", "¥" },
            { "cny", "¥" },
            { "krw", "₩" },
            { "inr", "₹" },
            { "cad", "C$" },
            { "aud", "A$" },
            { "chf", "CHF" },
            { "btc", "₿" },
            { "eth", "Ξ" }
        };
    }
}
