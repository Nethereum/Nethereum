namespace Nethereum.Wallet.UI.Components.Utils
{
    public static class CurrencyFormatter
    {
        public static string FormatPrice(decimal? price, string currencySymbol = "$")
        {
            if (!price.HasValue || price.Value == 0)
                return "-";

            var value = price.Value;
            var absValue = System.Math.Abs(value);

            if (absValue >= 1)
                return $"{currencySymbol}{value:N2}";
            if (absValue >= 0.01m)
                return $"{currencySymbol}{value:N4}";
            if (absValue >= 0.0001m)
                return $"{currencySymbol}{value:N6}";

            return $"{currencySymbol}{value:N8}";
        }

        public static string FormatValue(decimal? value, string currencySymbol = "$")
        {
            if (!value.HasValue)
                return "-";

            var val = value.Value;
            if (val == 0)
                return $"{currencySymbol}0.00";

            var absValue = System.Math.Abs(val);

            if (absValue >= 1)
                return $"{currencySymbol}{val:N2}";
            if (absValue >= 0.01m)
                return $"{currencySymbol}{val:N4}";
            if (absValue >= 0.0001m)
                return $"{currencySymbol}{val:N6}";

            return $"{currencySymbol}{val:N8}";
        }

        public static string FormatBalance(decimal balance, int maxDecimals = 8)
        {
            if (balance == 0)
                return "0";

            var absBalance = System.Math.Abs(balance);

            if (absBalance >= 1000)
                return $"{balance:N2}";
            if (absBalance >= 1)
                return $"{balance:N4}";
            if (absBalance >= 0.0001m)
                return $"{balance:N6}";

            return balance.ToString($"N{maxDecimals}").TrimEnd('0').TrimEnd('.');
        }
    }
}
