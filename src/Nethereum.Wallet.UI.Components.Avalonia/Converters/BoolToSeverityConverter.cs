using Avalonia.Data.Converters;
using Nethereum.Wallet.UI.Components.Avalonia.Views.Shared;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Nethereum.Wallet.UI.Components.Avalonia.Converters
{
    public class BoolToSeverityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                if (parameter is string severityString)
                {
                    if (Enum.TryParse(severityString, true, out WalletInfoSeverity severity))
                    {
                        return boolValue ? severity : WalletInfoSeverity.Info; // Default to Info if parameter is not provided or invalid
                    }
                }
                return boolValue ? WalletInfoSeverity.Info : WalletInfoSeverity.Warning; // Default if no parameter
            }
            return WalletInfoSeverity.Info; // Default if not a boolean
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
