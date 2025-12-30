using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Holdings
{
    public class HoldingsPluginLocalizer : ComponentLocalizerBase<HoldingsPluginViewModel>
    {
        public static class Keys
        {
            public const string DisplayName = "DisplayName";
            public const string Description = "Description";
        }

        public HoldingsPluginLocalizer(IWalletLocalizationService localizationService)
            : base(localizationService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "Holdings",
                [Keys.Description] = "View consolidated token holdings across all accounts and chains"
            });

            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "Activos",
                [Keys.Description] = "Ver activos de tokens consolidados en todas las cuentas y cadenas"
            });
        }
    }
}
