using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Nethereum.Wallet.UI.Components.Avalonia.Converters
{
    public class FormatAddressConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string address)
            {
                if (string.IsNullOrEmpty(address) || address.Length <= 16)
                    return address;

                return $"{address.Substring(0, 8)}...{address.Substring(address.Length - 6)}";
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
