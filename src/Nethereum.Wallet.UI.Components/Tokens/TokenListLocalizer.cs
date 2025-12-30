using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Tokens
{
    public class TokenListLocalizer : ComponentLocalizerBase<TokenListViewModel>
    {
        public static class Keys
        {
            public const string Tokens = "Tokens";
            public const string TotalValue = "TotalValue";
            public const string SearchTokens = "SearchTokens";
            public const string ShowZeroBalances = "ShowZeroBalances";
            public const string ScanAllTokens = "ScanAllTokens";
            public const string RefreshBalances = "RefreshBalances";
            public const string RefreshPrices = "RefreshPrices";
            public const string AddCustomToken = "AddCustomToken";
            public const string Settings = "Settings";
            public const string NoTokensFound = "NoTokensFound";
            public const string Scanning = "Scanning";
            public const string Loading = "Loading";
            public const string NoAccountSelected = "NoAccountSelected";
            public const string Balance = "Balance";
            public const string Price = "Price";
            public const string Value = "Value";
            public const string HideToken = "HideToken";
            public const string ShowToken = "ShowToken";
            public const string CustomToken = "CustomToken";
            public const string TokenDetails = "TokenDetails";
            public const string Error = "Error";
        }

        public TokenListLocalizer(IWalletLocalizationService localizationService)
            : base(localizationService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.Tokens] = "Tokens",
                [Keys.TotalValue] = "Total Value",
                [Keys.SearchTokens] = "Search tokens...",
                [Keys.ShowZeroBalances] = "Show zero balances",
                [Keys.ScanAllTokens] = "Scan All Tokens",
                [Keys.RefreshBalances] = "Refresh Balances",
                [Keys.RefreshPrices] = "Refresh Prices",
                [Keys.AddCustomToken] = "Add Custom Token",
                [Keys.Settings] = "Settings",
                [Keys.NoTokensFound] = "No tokens found",
                [Keys.Scanning] = "Scanning",
                [Keys.Loading] = "Loading...",
                [Keys.NoAccountSelected] = "Please select an account first",
                [Keys.Balance] = "Balance",
                [Keys.Price] = "Price",
                [Keys.Value] = "Value",
                [Keys.HideToken] = "Hide token",
                [Keys.ShowToken] = "Show token",
                [Keys.CustomToken] = "Custom",
                [Keys.TokenDetails] = "Token Details",
                [Keys.Error] = "Error"
            });

            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.Tokens] = "Tokens",
                [Keys.TotalValue] = "Valor Total",
                [Keys.SearchTokens] = "Buscar tokens...",
                [Keys.ShowZeroBalances] = "Mostrar saldos cero",
                [Keys.ScanAllTokens] = "Escanear Todos los Tokens",
                [Keys.RefreshBalances] = "Actualizar Saldos",
                [Keys.RefreshPrices] = "Actualizar Precios",
                [Keys.AddCustomToken] = "Agregar Token Personalizado",
                [Keys.Settings] = "Configuración",
                [Keys.NoTokensFound] = "No se encontraron tokens",
                [Keys.Scanning] = "Escaneando",
                [Keys.Loading] = "Cargando...",
                [Keys.NoAccountSelected] = "Por favor seleccione una cuenta primero",
                [Keys.Balance] = "Saldo",
                [Keys.Price] = "Precio",
                [Keys.Value] = "Valor",
                [Keys.HideToken] = "Ocultar token",
                [Keys.ShowToken] = "Mostrar token",
                [Keys.CustomToken] = "Personalizado",
                [Keys.TokenDetails] = "Detalles del Token",
                [Keys.Error] = "Error"
            });
        }
    }
}
