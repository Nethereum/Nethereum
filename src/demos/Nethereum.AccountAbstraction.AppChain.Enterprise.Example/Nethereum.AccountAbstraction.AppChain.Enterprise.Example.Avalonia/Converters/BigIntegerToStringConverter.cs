using System;
using System.Globalization;
using System.Numerics;
using Avalonia.Data.Converters;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Avalonia.Converters;

public sealed class BigIntegerToStringConverter : IValueConverter
{
    public static readonly BigIntegerToStringConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is BigInteger big ? big.ToString() : "0";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string text && BigInteger.TryParse(text, out var result) ? result : BigInteger.Zero;
}
