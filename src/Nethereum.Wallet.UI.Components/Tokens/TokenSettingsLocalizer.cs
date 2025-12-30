using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Tokens
{
    public class TokenSettingsLocalizer : ComponentLocalizerBase<TokenSettingsViewModel>
    {
        public static class Keys
        {
            public const string Title = "Title";
            public const string Currency = "Currency";
            public const string CurrencyDescription = "CurrencyDescription";
            public const string RefreshInterval = "RefreshInterval";
            public const string RefreshIntervalDescription = "RefreshIntervalDescription";
            public const string AutoRefreshPrices = "AutoRefreshPrices";
            public const string AutoRefreshPricesDescription = "AutoRefreshPricesDescription";
            public const string Save = "Save";
            public const string Cancel = "Cancel";
            public const string SettingsSaved = "SettingsSaved";
            public const string DisplaySection = "DisplaySection";
            public const string RefreshSection = "RefreshSection";
            public const string Seconds = "Seconds";
            public const string Error = "Error";
            public const string Success = "Success";
        }

        public TokenSettingsLocalizer(IWalletLocalizationService localizationService)
            : base(localizationService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.Title] = "Token Settings",
                [Keys.Currency] = "Display Currency",
                [Keys.CurrencyDescription] = "Select the currency for displaying token values",
                [Keys.RefreshInterval] = "Refresh Interval",
                [Keys.RefreshIntervalDescription] = "How often to refresh token prices (in seconds)",
                [Keys.AutoRefreshPrices] = "Auto-refresh Prices",
                [Keys.AutoRefreshPricesDescription] = "Automatically refresh token prices at the specified interval",
                [Keys.Save] = "Save Settings",
                [Keys.Cancel] = "Cancel",
                [Keys.SettingsSaved] = "Settings saved successfully",
                [Keys.DisplaySection] = "Display",
                [Keys.RefreshSection] = "Refresh Options",
                [Keys.Seconds] = "seconds",
                [Keys.Error] = "Error",
                [Keys.Success] = "Success"
            });

            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.Title] = "Configuración de Tokens",
                [Keys.Currency] = "Moneda de Visualización",
                [Keys.CurrencyDescription] = "Seleccione la moneda para mostrar valores de tokens",
                [Keys.RefreshInterval] = "Intervalo de Actualización",
                [Keys.RefreshIntervalDescription] = "Frecuencia de actualización de precios (en segundos)",
                [Keys.AutoRefreshPrices] = "Actualización Automática de Precios",
                [Keys.AutoRefreshPricesDescription] = "Actualizar automáticamente los precios al intervalo especificado",
                [Keys.Save] = "Guardar Configuración",
                [Keys.Cancel] = "Cancelar",
                [Keys.SettingsSaved] = "Configuración guardada exitosamente",
                [Keys.DisplaySection] = "Visualización",
                [Keys.RefreshSection] = "Opciones de Actualización",
                [Keys.Seconds] = "segundos",
                [Keys.Error] = "Error",
                [Keys.Success] = "Exito"
            });
        }
    }
}
