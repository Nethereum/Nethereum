using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace Nethereum.Wallet.UI.Components.Avalonia.Converters
{
    public class ColorLightenConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ISolidColorBrush brush)
            {
                var color = brush.Color;
                // Lighten the color by increasing the RGB values towards white
                var r = Math.Min(255, color.R + (255 - color.R) * 0.2);
                var g = Math.Min(255, color.G + (255 - color.G) * 0.2);
                var b = Math.Min(255, color.B + (255 - color.B) * 0.2);

                return new SolidColorBrush(Color.FromArgb(color.A, (byte)r, (byte)g, (byte)b));
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
