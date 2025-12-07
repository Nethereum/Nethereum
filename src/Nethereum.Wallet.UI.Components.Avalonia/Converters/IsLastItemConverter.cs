using Avalonia.Data.Converters;
using System;
using System.Collections;
using System.Globalization;
using System.Linq;

namespace Nethereum.Wallet.UI.Components.Avalonia.Converters
{
    public class IsLastItemConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Count == 2 && values[0] != null && values[1] is IEnumerable collection)
            {
                var currentItem = values[0];
                var lastItem = collection.Cast<object>().LastOrDefault();
                return Equals(currentItem, lastItem);
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}