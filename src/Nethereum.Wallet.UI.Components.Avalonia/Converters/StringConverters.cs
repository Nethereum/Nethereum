using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Nethereum.Wallet.UI.Components.Avalonia.Converters
{
    public static class StringConverters
    {
        public static IValueConverter IsNullOrEmpty =>
            new FuncValueConverter<string, bool>(string.IsNullOrEmpty);

        public static IValueConverter IsNotNullOrEmpty =>
            new FuncValueConverter<string, bool>(s => !string.IsNullOrEmpty(s));
    }
}
