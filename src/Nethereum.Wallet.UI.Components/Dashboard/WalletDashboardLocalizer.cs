using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Dashboard
{
    public class WalletDashboardLocalizer : ComponentLocalizerBase<WalletDashboardViewModel>
    {
        public static class Keys
        {
            public const string WalletDashboard = "WalletDashboard";
            public const string NoAccount = "NoAccount";
            public const string SelectSection = "SelectSection";
            public const string SelectSectionMessage = "SelectSectionMessage";
            public const string Account = "Account";
            public const string Loading = "Loading";
            public const string Dashboard = "Dashboard";
            public const string Menu = "Menu";
            public const string Close = "Close";
            public const string Navigation = "Navigation";
            public const string Logout = "Logout";
            public const string LegacySeedWarning = "LegacySeedWarning";
        }

        public WalletDashboardLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.WalletDashboard] = "Wallet Dashboard",
                [Keys.NoAccount] = "No Account",
                [Keys.SelectSection] = "Select a section",
                [Keys.SelectSectionMessage] = "Select a section from the menu to get started",
                [Keys.Account] = "Account",
                [Keys.Loading] = "Loading...",
                [Keys.Dashboard] = "Dashboard",
                [Keys.Menu] = "Menu",
                [Keys.Close] = "Close",
                [Keys.Navigation] = "Navigation",
                [Keys.Logout] = "Logout",
                [Keys.LegacySeedWarning] = "This wallet's recovery phrase is stored in a non-standard form from an earlier version. Restoring the phrase in another wallet will not show these funds. Create a new wallet and move your funds to it."
            });
            
            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.WalletDashboard] = "Panel de Cartera",
                [Keys.NoAccount] = "Sin Cuenta",
                [Keys.SelectSection] = "Selecciona una sección",
                [Keys.SelectSectionMessage] = "Selecciona una sección del menú para comenzar",
                [Keys.Account] = "Cuenta",
                [Keys.Loading] = "Cargando...",
                [Keys.Dashboard] = "Panel",
                [Keys.Menu] = "Menú",
                [Keys.Close] = "Cerrar",
                [Keys.Navigation] = "Navegación",
                [Keys.Logout] = "Cerrar sesión",
                [Keys.LegacySeedWarning] = "La frase de recuperación de esta cartera está guardada en una forma no estándar de una versión anterior. Restaurar la frase en otra cartera no mostrará estos fondos. Crea una cartera nueva y traslada tus fondos a ella."
            });
        }
    }
}
