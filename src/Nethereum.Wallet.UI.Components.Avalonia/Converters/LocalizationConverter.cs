using Avalonia.Data.Converters;
using Nethereum.Wallet.UI.Components.Core.Localization;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Nethereum.Wallet.UI.Components.Avalonia.Converters
{
    public class LocalizationConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Count >= 2 && values[0] is string key && values[1] is IComponentLocalizer localizer)
            {
                if (values.Count > 2 && values[2] is object[] args)
                {
                    return localizer.GetString(key, args);
                }
                return localizer.GetString(key);
            }
            return values.Count > 0 ? values[0] : string.Empty; // Fallback to key or empty string
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
