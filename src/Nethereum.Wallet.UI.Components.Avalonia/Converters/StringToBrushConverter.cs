using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Nethereum.Wallet.UI.Components.Avalonia.Converters
{
    public class StringToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorString)
            {
                return colorString?.ToLowerInvariant() switch
                {
                    "primary" => Brushes.Blue, // Placeholder, replace with actual primary color
                    "secondary" => Brushes.Gray, // Placeholder
                    "tertiary" => Brushes.LightGray, // Placeholder
                    "info" => Brushes.LightBlue, // Placeholder
                    "success" => Brushes.Green, // Placeholder
                    "warning" => Brushes.Orange, // Placeholder
                    "error" => Brushes.Red, // Placeholder
                    "dark" => Brushes.Black, // Placeholder
                    "default" => Brushes.Black, // Placeholder
                    "inherit" => Brushes.Black, // Placeholder
                    "surface" => Brushes.White, // Placeholder
                    "transparent" => Brushes.Transparent, // Placeholder
                    _ => Brushes.Black // Default
                };
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
