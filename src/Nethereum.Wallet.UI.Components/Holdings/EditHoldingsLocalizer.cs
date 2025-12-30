using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Holdings
{
    public class EditHoldingsLocalizer : ComponentLocalizerBase<EditHoldingsViewModel>
    {
        public static class Keys
        {
            public const string Title = "Title";
            public const string Accounts = "Accounts";
            public const string Networks = "Networks";
            public const string AccountsToScan = "AccountsToScan";
            public const string NetworksToScan = "NetworksToScan";
            public const string SelectAll = "SelectAll";
            public const string DeselectAll = "DeselectAll";
            public const string SelectionSummary = "SelectionSummary";
            public const string Save = "Save";
            public const string Cancel = "Cancel";
            public const string Loading = "Loading";
            public const string NoAccounts = "NoAccounts";
            public const string NoNetworks = "NoNetworks";
            public const string ViewOnly = "ViewOnly";
            public const string FullyScanned = "FullyScanned";
            public const string PartiallyScanned = "PartiallyScanned";
            public const string NotScanned = "NotScanned";
            public const string NeedsScan = "NeedsScan";
            public const string ScanSummaryFormat = "ScanSummaryFormat";
            public const string AllScanned = "AllScanned";
            public const string ChainsScanned = "ChainsScanned";
            public const string ForceRescan = "ForceRescan";
            public const string WillRescan = "WillRescan";
        }

        public EditHoldingsLocalizer(IWalletLocalizationService localizationService)
            : base(localizationService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.Title] = "Edit Holdings",
                [Keys.Accounts] = "Accounts",
                [Keys.Networks] = "Chains",
                [Keys.AccountsToScan] = "Accounts to Scan",
                [Keys.NetworksToScan] = "Chains to Scan",
                [Keys.SelectAll] = "Select All",
                [Keys.DeselectAll] = "Deselect All",
                [Keys.SelectionSummary] = "{0} accounts × {1} chains selected",
                [Keys.Save] = "Save",
                [Keys.Cancel] = "Cancel",
                [Keys.Loading] = "Loading...",
                [Keys.NoAccounts] = "No accounts available",
                [Keys.NoNetworks] = "No chains available",
                [Keys.ViewOnly] = "View-only",
                [Keys.FullyScanned] = "Fully scanned",
                [Keys.PartiallyScanned] = "Partially scanned",
                [Keys.NotScanned] = "Not scanned",
                [Keys.NeedsScan] = "Needs scanning",
                [Keys.ScanSummaryFormat] = "Saving will scan: {0} accounts × {1} chains = {2} scans",
                [Keys.AllScanned] = "All selected accounts are fully scanned",
                [Keys.ChainsScanned] = "{0} chains scanned",
                [Keys.ForceRescan] = "Force rescan",
                [Keys.WillRescan] = "Will rescan from scratch"
            });

            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.Title] = "Editar Tenencias",
                [Keys.Accounts] = "Cuentas",
                [Keys.Networks] = "Cadenas",
                [Keys.AccountsToScan] = "Cuentas a Escanear",
                [Keys.NetworksToScan] = "Cadenas a Escanear",
                [Keys.SelectAll] = "Seleccionar Todo",
                [Keys.DeselectAll] = "Deseleccionar Todo",
                [Keys.SelectionSummary] = "{0} cuentas × {1} cadenas seleccionadas",
                [Keys.Save] = "Guardar",
                [Keys.Cancel] = "Cancelar",
                [Keys.Loading] = "Cargando...",
                [Keys.NoAccounts] = "No hay cuentas disponibles",
                [Keys.NoNetworks] = "No hay cadenas disponibles",
                [Keys.ViewOnly] = "Solo lectura",
                [Keys.FullyScanned] = "Completamente escaneado",
                [Keys.PartiallyScanned] = "Parcialmente escaneado",
                [Keys.NotScanned] = "No escaneado",
                [Keys.NeedsScan] = "Necesita escaneo",
                [Keys.ScanSummaryFormat] = "Guardar escaneará: {0} cuentas × {1} cadenas = {2} escaneos",
                [Keys.AllScanned] = "Todas las cuentas seleccionadas están completamente escaneadas",
                [Keys.ChainsScanned] = "{0} cadenas escaneadas",
                [Keys.ForceRescan] = "Forzar reescaneo",
                [Keys.WillRescan] = "Se reescaneará desde cero"
            });
        }
    }
}
