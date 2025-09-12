using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Networks
{
    public class NetworkManagementPluginLocalizer : ComponentLocalizerBase<NetworkManagementPluginViewModel>
    {
        public static class Keys
        {
            public const string DisplayName = "DisplayName";
            public const string Description = "Description";
        }

        public NetworkManagementPluginLocalizer(IWalletLocalizationService localizationService) 
            : base(localizationService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "Networks",
                [Keys.Description] = "Manage and configure blockchain networks"
            });
            
            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "Redes",
                [Keys.Description] = "Gestionar y configurar redes blockchain"
            });
        }
    }
}