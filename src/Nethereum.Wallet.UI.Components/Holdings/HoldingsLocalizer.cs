using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Holdings
{
    public class HoldingsLocalizer : ComponentLocalizerBase<HoldingsViewModel>
    {
        public static class Keys
        {
            public const string Title = "Title";
            public const string TotalValue = "TotalValue";
            public const string Edit = "Edit";
            public const string Refresh = "Refresh";
            public const string Refreshing = "Refreshing";
            public const string Scan = "Scan";
            public const string Scanning = "Scanning";
            public const string Accounts = "Accounts";
            public const string Networks = "Networks";
            public const string Tokens = "Tokens";
            public const string Portfolios = "Portfolios";
            public const string NoAccountsOrNetworksSelected = "NoAccountsOrNetworksSelected";
            public const string NoHoldingsYet = "NoHoldingsYet";
            public const string NoHoldingsDescription = "NoHoldingsDescription";
            public const string ConfigureHoldings = "ConfigureHoldings";
            public const string LastUpdated = "LastUpdated";
            public const string Loading = "Loading";
            public const string ErrorLoading = "ErrorLoading";
            public const string AccountsCount = "AccountsCount";
            public const string NetworksCount = "NetworksCount";
            public const string TokensCount = "TokensCount";
            public const string ViewAll = "ViewAll";
            public const string CreatePortfolio = "CreatePortfolio";
            public const string NotSelected = "NotSelected";
            public const string Scanned = "Scanned";
            public const string NotScanned = "NotScanned";
            public const string Cancel = "Cancel";
            public const string Save = "Save";
            public const string NoAccountsConfigured = "NoAccountsConfigured";
            public const string NoNetworksConfigured = "NoNetworksConfigured";
            public const string TokenDetailsPlaceholder = "TokenDetailsPlaceholder";
            public const string NoTokensFound = "NoTokensFound";
            public const string ScanToDiscover = "ScanToDiscover";
            public const string NoPortfoliosYet = "NoPortfoliosYet";
            public const string CreatePortfolioDescription = "CreatePortfolioDescription";
            public const string ScanningProgress = "ScanningProgress";
            public const string Send = "Send";
            public const string SendOnChain = "SendOnChain";
            public const string AddToken = "AddToken";
            public const string UpdatingPrices = "UpdatingPrices";
            public const string RefreshComplete = "RefreshComplete";
            public const string ScanningTransfers = "ScanningTransfers";
            public const string UpdatingBalances = "UpdatingBalances";
            public const string SearchTokens = "SearchTokens";
        }

        public HoldingsLocalizer(IWalletLocalizationService localizationService)
            : base(localizationService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.Title] = "Holdings",
                [Keys.TotalValue] = "Total Value",
                [Keys.Edit] = "Edit / Rescan",
                [Keys.Refresh] = "Refresh",
                [Keys.Refreshing] = "Refreshing...",
                [Keys.Scan] = "Scan",
                [Keys.Scanning] = "Scanning...",
                [Keys.Accounts] = "Accounts",
                [Keys.Networks] = "Chains",
                [Keys.Tokens] = "Tokens",
                [Keys.Portfolios] = "Portfolios",
                [Keys.NoAccountsOrNetworksSelected] = "Please select at least one account and one chain",
                [Keys.NoHoldingsYet] = "No Holdings Configured",
                [Keys.NoHoldingsDescription] = "Configure which accounts and chains to track",
                [Keys.ConfigureHoldings] = "Configure Holdings",
                [Keys.LastUpdated] = "Updated {0}",
                [Keys.Loading] = "Loading holdings...",
                [Keys.ErrorLoading] = "Failed to load holdings",
                [Keys.AccountsCount] = "{0} accounts",
                [Keys.NetworksCount] = "{0} chains",
                [Keys.TokensCount] = "{0} tokens",
                [Keys.ViewAll] = "View All",
                [Keys.CreatePortfolio] = "Create Portfolio",
                [Keys.NotSelected] = "Not selected",
                [Keys.Scanned] = "Scanned {0}",
                [Keys.NotScanned] = "Not scanned",
                [Keys.Cancel] = "Cancel",
                [Keys.Save] = "Save",
                [Keys.NoAccountsConfigured] = "No accounts configured",
                [Keys.NoNetworksConfigured] = "No chains configured",
                [Keys.TokenDetailsPlaceholder] = "Token details will be shown here when expanded",
                [Keys.NoTokensFound] = "No tokens found",
                [Keys.ScanToDiscover] = "Scan to discover tokens across your accounts and chains",
                [Keys.NoPortfoliosYet] = "No portfolios yet",
                [Keys.CreatePortfolioDescription] = "Create a portfolio to group your accounts",
                [Keys.ScanningProgress] = "Scanning progress",
                [Keys.Send] = "Send",
                [Keys.SendOnChain] = "Send on this chain",
                [Keys.AddToken] = "Add Token",
                [Keys.UpdatingPrices] = "Refreshing prices...",
                [Keys.RefreshComplete] = "Refresh complete",
                [Keys.ScanningTransfers] = "Scanning latest transfers...",
                [Keys.UpdatingBalances] = "Refreshing existing balances...",
                [Keys.SearchTokens] = "Search tokens..."
            });

            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.Title] = "Activos",
                [Keys.TotalValue] = "Valor Total",
                [Keys.Edit] = "Editar / Reescanear",
                [Keys.Refresh] = "Actualizar",
                [Keys.Refreshing] = "Actualizando...",
                [Keys.Scan] = "Escanear",
                [Keys.Scanning] = "Escaneando...",
                [Keys.Accounts] = "Cuentas",
                [Keys.Networks] = "Cadenas",
                [Keys.Tokens] = "Tokens",
                [Keys.Portfolios] = "Portafolios",
                [Keys.NoAccountsOrNetworksSelected] = "Por favor selecciona al menos una cuenta y una cadena",
                [Keys.NoHoldingsYet] = "Sin Tenencias Configuradas",
                [Keys.NoHoldingsDescription] = "Configura qué cuentas y cadenas rastrear",
                [Keys.ConfigureHoldings] = "Configurar Tenencias",
                [Keys.LastUpdated] = "Actualizado {0}",
                [Keys.Loading] = "Cargando tenencias...",
                [Keys.ErrorLoading] = "Error al cargar tenencias",
                [Keys.AccountsCount] = "{0} cuentas",
                [Keys.NetworksCount] = "{0} cadenas",
                [Keys.TokensCount] = "{0} tokens",
                [Keys.ViewAll] = "Ver Todo",
                [Keys.CreatePortfolio] = "Crear Portafolio",
                [Keys.NotSelected] = "No seleccionado",
                [Keys.Scanned] = "Escaneado {0}",
                [Keys.NotScanned] = "No escaneado",
                [Keys.Cancel] = "Cancelar",
                [Keys.Save] = "Guardar",
                [Keys.NoAccountsConfigured] = "Sin cuentas configuradas",
                [Keys.NoNetworksConfigured] = "Sin cadenas configuradas",
                [Keys.TokenDetailsPlaceholder] = "Los detalles del token se mostrarán aquí cuando se expanda",
                [Keys.NoTokensFound] = "No se encontraron tokens",
                [Keys.ScanToDiscover] = "Escanea para descubrir tokens en tus cuentas y cadenas",
                [Keys.NoPortfoliosYet] = "Aún no hay portafolios",
                [Keys.CreatePortfolioDescription] = "Crea un portafolio para agrupar tus cuentas",
                [Keys.ScanningProgress] = "Progreso del escaneo",
                [Keys.Send] = "Enviar",
                [Keys.SendOnChain] = "Enviar en esta cadena",
                [Keys.AddToken] = "Añadir Token",
                [Keys.UpdatingPrices] = "Actualizando precios...",
                [Keys.RefreshComplete] = "Actualización completa",
                [Keys.ScanningTransfers] = "Escaneando últimas transferencias...",
                [Keys.UpdatingBalances] = "Actualizando saldos existentes...",
                [Keys.SearchTokens] = "Buscar tokens..."
            });
        }
    }
}
