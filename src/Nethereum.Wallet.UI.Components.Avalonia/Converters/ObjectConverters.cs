using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Nethereum.Wallet.UI.Components.Avalonia.Converters
{
    public static class ObjectConverters
    {
        public static readonly IValueConverter IsEqual = new FuncValueConverter<object, object, bool>((value, parameter) =>
        {
            if (value == null && parameter == null) return true;
            if (value == null || parameter == null) return false;
            return value.Equals(parameter);
        });

        public static readonly IValueConverter IsNotEqual = new FuncValueConverter<object, object, bool>((value, parameter) =>
        {
            if (value == null && parameter == null) return false;
            if (value == null || parameter == null) return true;
            return !value.Equals(parameter);
        });

        public static readonly IValueConverter IsGreaterThan = new FuncValueConverter<object, object, bool>((value, parameter) =>
        {
            if (value == null || parameter == null) return false;

            try
            {
                var val = Convert.ToDouble(value);
                var param = Convert.ToDouble(parameter);
                return val > param;
            }
            catch
            {
                return false;
            }
        });

        public static readonly IValueConverter IsLessThan = new FuncValueConverter<object, object, bool>((value, parameter) =>
        {
            if (value == null || parameter == null) return false;

            try
            {
                var val = Convert.ToDouble(value);
                var param = Convert.ToDouble(parameter);
                return val < param;
            }
            catch
            {
                return false;
            }
        });

        public static readonly IValueConverter Multiply = new FuncValueConverter<object, object, object>((value, parameter) =>
        {
            if (value == null || parameter == null) return 0;

            try
            {
                var val = Convert.ToDouble(value);
                var param = Convert.ToDouble(parameter);
                return val * param;
            }
            catch
            {
                return 0;
            }
        });

        public static readonly IValueConverter Add = new FuncValueConverter<object, object, object>((value, parameter) =>
        {
            if (value == null || parameter == null) return 0;

            try
            {
                var val = Convert.ToDouble(value);
                var param = Convert.ToDouble(parameter);
                return val + param;
            }
            catch
            {
                return 0;
            }
        });

        public static readonly IValueConverter Subtract = new FuncValueConverter<object, object, object>((value, parameter) =>
        {
            if (value == null || parameter == null) return 0;

            try
            {
                var val = Convert.ToDouble(value);
                var param = Convert.ToDouble(parameter);
                return val - param;
            }
            catch
            {
                return 0;
            }
        });

        public static readonly IValueConverter IsNotNullOrEmpty = new FuncValueConverter<object, bool>(value =>
        {
            if (value == null) return false;
            if (value is string str) return !string.IsNullOrEmpty(str);
            return true;
        });

        public static readonly IValueConverter IsNullOrEmpty = new FuncValueConverter<object, bool>(value =>
        {
            if (value == null) return true;
            if (value is string str) return string.IsNullOrEmpty(str);
            return false;
        });

        public static readonly IValueConverter IconNameToIconData = new FuncValueConverter<string, string>(iconName =>
        {
            return Extensions.IconMappingExtensions.ToAvaloniaPathIconData(iconName);
        });

        public static readonly IValueConverter BoolInvert = new FuncValueConverter<bool, bool>(value => !value);
    }

    public class FuncValueConverter<TIn, TParam, TOut> : IValueConverter
    {
        private readonly Func<TIn, TParam, TOut> _convert;

        public FuncValueConverter(Func<TIn, TParam, TOut> convert)
        {
            _convert = convert;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return _convert((TIn)value, (TParam)parameter);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class FuncValueConverter<TIn, TOut> : IValueConverter
    {
        private readonly Func<TIn, TOut> _convert;

        public FuncValueConverter(Func<TIn, TOut> convert)
        {
            _convert = convert;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return _convert((TIn)value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}