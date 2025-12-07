using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Nethereum.Wallet.UI.Components.Avalonia.Converters
{
    public class BoolToBrushConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Count == 3 && values[0] is bool boolValue && values[1] is string trueBrushName && values[2] is string falseBrushName)
            {
                var app = global::Avalonia.Application.Current;
                if (app != null && app.TryGetResource(trueBrushName, ThemeVariant.Default, out var trueBrush) && app.TryGetResource(falseBrushName, ThemeVariant.Default, out var falseBrush))
                {
                    return boolValue ? trueBrush : falseBrush;
                }
            }
            return Brushes.Transparent; // Default or fallback brush
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
