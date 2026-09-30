using System;
using System.Globalization;
using System.Numerics;
using Avalonia.Data.Converters;

namespace Nethereum.AccountAbstraction.Example.Avalonia.Converters;

public sealed class StringToBigIntegerConverter : IValueConverter
{
    public static readonly StringToBigIntegerConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string text && BigInteger.TryParse(text, out var result) ? result : BigInteger.Zero;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString();
}
