using Avalonia.Data.Converters;
using System;
using System.Collections;
using System.Globalization;
using System.Linq;

namespace Nethereum.Wallet.UI.Components.Avalonia.Converters
{
    public class IsFirstItemConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Count == 2 && values[0] != null && values[1] is IEnumerable collection)
            {
                var currentItem = values[0];
                var firstItem = collection.Cast<object>().FirstOrDefault();
                return Equals(currentItem, firstItem);
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
