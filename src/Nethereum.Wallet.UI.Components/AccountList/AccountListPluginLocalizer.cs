using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.AccountList
{
    public class AccountListPluginLocalizer : ComponentLocalizerBase<AccountListPluginViewModel>
    {
        public static class Keys
        {
            public const string DisplayName = "DisplayName";
            public const string Description = "Description";
        }

        public AccountListPluginLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "My Accounts",
                [Keys.Description] = "View and manage your wallet accounts"
            });
            
            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "Mis Cuentas",
                [Keys.Description] = "Ver y gestionar las cuentas de tu cartera"
            });
        }
    }
}