using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Nethereum.Wallet.UI.Components.Avalonia.Converters
{
    public class BoolToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string stringValue)
            {
                return boolValue ? stringValue : string.Empty;
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
