using Avalonia.Data.Converters;
using Avalonia.Media;
using Nethereum.Wallet.UI.Components.WalletAccounts;
using System;
using System.Globalization;

namespace Nethereum.Wallet.UI.Components.Avalonia.Converters
{
    public class AccountTypeToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IAccountCreationViewModel accountType)
            {
                var typeName = accountType.GetType().Name.ToLowerInvariant();
                var hash1 = 0;
                var hash2 = 0;

                for (int i = 0; i < Math.Min(typeName.Length, 8); i++)
                {
                    hash1 = hash1 * 31 + typeName[i];
                    if (i + 8 < typeName.Length)
                        hash2 = hash2 * 31 + typeName[i + 8];
                }

                var hue = Math.Abs(hash1) % 360;
                var saturation = 65 + (Math.Abs(hash2) % 20);
                var lightness = 45 + (Math.Abs(hash1 >> 8) % 20);

                var color = HslToRgb(hue / 360.0, saturation / 100.0, lightness / 100.0);
                return new SolidColorBrush(color);
            }

            return new SolidColorBrush(Colors.Gray);
        }

        private static Color HslToRgb(double h, double s, double l)
        {
            double r, g, b;

            if (s == 0)
            {
                r = g = b = l; // achromatic
            }
            else
            {
                var hue2rgb = new Func<double, double, double, double>((p, q, t) =>
                {
                    if (t < 0) t += 1;
                    if (t > 1) t -= 1;
                    if (t < 1.0 / 6) return p + (q - p) * 6 * t;
                    if (t < 1.0 / 2) return q;
                    if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
                    return p;
                });

                var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                var p = 2 * l - q;
                r = hue2rgb(p, q, h + 1.0 / 3);
                g = hue2rgb(p, q, h);
                b = hue2rgb(p, q, h - 1.0 / 3);
            }

            return Color.FromArgb(255,
                (byte)Math.Round(r * 255),
                (byte)Math.Round(g * 255),
                (byte)Math.Round(b * 255));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
