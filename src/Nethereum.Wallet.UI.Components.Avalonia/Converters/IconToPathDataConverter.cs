using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Nethereum.Wallet.UI.Components.Avalonia.Converters
{
    public class IconToPathDataConverter : IValueConverter
    {
        public static readonly IconToPathDataConverter Instance = new IconToPathDataConverter();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Prefer explicit parameter (e.g., ConverterParameter="history")
            var iconId = parameter as string ?? value as string;
            if (string.IsNullOrWhiteSpace(iconId)) return null;
            return Extensions.IconMappingExtensions.ToAvaloniaPathIconData(iconId);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
