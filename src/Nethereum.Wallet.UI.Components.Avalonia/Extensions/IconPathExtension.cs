using Avalonia.Markup.Xaml;
using System;

namespace Nethereum.Wallet.UI.Components.Avalonia.Extensions
{
    public class IconPathExtension : MarkupExtension
    {
        public string IconName { get; set; }

        public IconPathExtension() { }

        public IconPathExtension(string iconName)
        {
            IconName = iconName;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(IconName))
                return string.Empty;

            return IconMappingExtensions.ToAvaloniaPathIconData(IconName);
        }
    }
}