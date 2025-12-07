using Avalonia.Data.Converters;
using Nethereum.Wallet.UI.Components.Avalonia.Extensions;
using System;
using System.Globalization;

namespace Nethereum.Wallet.UI.Components.Avalonia.Converters
{
    public class IconMappingConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var iconIdentifier = value?.ToString();
            if (string.IsNullOrEmpty(iconIdentifier))
                return null;

            return iconIdentifier.ToAvaloniaPathIconData();
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}