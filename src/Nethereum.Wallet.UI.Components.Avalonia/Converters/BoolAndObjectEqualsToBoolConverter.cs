using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Nethereum.Wallet.UI.Components.Avalonia.Converters
{
    public class BoolAndObjectEqualsToBoolConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Count >= 3 && values[0] is bool boolValue && values[1] != null && values[2] != null)
            {
                return boolValue && values[1].Equals(values[2]);
            }
            return false;
        }
    }
}
