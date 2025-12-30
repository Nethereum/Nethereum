using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.WalletOverview
{
    public class WalletOverviewPluginLocalizer : ComponentLocalizerBase<WalletOverviewPluginViewModel>
    {
        public static class Keys
        {
            public const string DisplayName = "DisplayName";
            public const string Description = "Description";
        }

        public WalletOverviewPluginLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "Current Account",
                [Keys.Description] = "View your current account balance and activity"
            });

            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "Cuenta Activa",
                [Keys.Description] = "Ver el saldo y actividad de tu cuenta activa"
            });
        }
    }
}