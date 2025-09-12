using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.CreateAccount
{
    public class CreateAccountPluginLocalizer : ComponentLocalizerBase<CreateAccountPluginViewModel>
    {
        public static class Keys
        {
            public const string DisplayName = "DisplayName";
            public const string Description = "Description";
        }

        public CreateAccountPluginLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "Add Account",
                [Keys.Description] = "Create or import a new account"
            });
            
            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "Agregar Cuenta",
                [Keys.Description] = "Crear o importar una nueva cuenta"
            });
        }
    }
}