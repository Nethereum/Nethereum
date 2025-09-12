using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.WalletOverview
{
    public class WalletOverviewLocalizer : ComponentLocalizerBase<WalletOverviewViewModel>
    {
        public static class Keys
        {
            public const string Title = "Title";
            public const string WalletOverview = "WalletOverview";
            public const string Balance = "Balance";
            public const string TotalBalance = "TotalBalance";
            public const string RecentTransactions = "RecentTransactions";
            public const string NoTransactions = "NoTransactions";
            public const string SendButton = "SendButton";
            public const string ReceiveButton = "ReceiveButton";
            public const string ManageAccountsButton = "ManageAccountsButton";
            public const string CopyAddressButton = "CopyAddressButton";
            public const string RefreshButton = "RefreshButton";
            public const string NetworkLabel = "NetworkLabel";
            public const string AccountLabel = "AccountLabel";
            public const string AddressCopied = "AddressCopied";
            public const string RefreshSuccess = "RefreshSuccess";
            public const string RefreshError = "RefreshError";
            public const string AccountSwitched = "AccountSwitched";
            public const string SelectAccountFirst = "SelectAccountFirst";
            public const string BalanceLoadError = "BalanceLoadError";
            public const string PriceUnavailable = "PriceUnavailable";
            public const string ErrorText = "ErrorText";
            public const string NetworkSwitched = "NetworkSwitched";
            public const string LoadingBalance = "LoadingBalance";
            public const string LoadingTransactions = "LoadingTransactions";
            public const string TransactionHistory = "TransactionHistory";
            public const string TokenBalances = "TokenBalances";
            public const string PendingTransactions = "PendingTransactions";
            public const string ViewAll = "ViewAll";
            public const string BackButton = "BackButton";
            public const string NoExplorerConfigured = "NoExplorerConfigured";
            public const string FailedToOpenExplorer = "FailedToOpenExplorer";
            public const string TransactionHashCopied = "TransactionHashCopied";
        }

        public WalletOverviewLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.Title] = "Wallet Overview",
                [Keys.WalletOverview] = "Wallet Overview",
                [Keys.Balance] = "Balance",
                [Keys.TotalBalance] = "Total Balance",
                [Keys.RecentTransactions] = "Recent Transactions",
                [Keys.NoTransactions] = "No recent transactions found",
                [Keys.SendButton] = "Send",
                [Keys.ReceiveButton] = "Receive",
                [Keys.ManageAccountsButton] = "Manage Accounts",
                [Keys.CopyAddressButton] = "Copy Address",
                [Keys.RefreshButton] = "Refresh",
                [Keys.NetworkLabel] = "Network",
                [Keys.AccountLabel] = "Account",
                [Keys.AddressCopied] = "Address copied to clipboard",
                [Keys.RefreshSuccess] = "Wallet overview refreshed successfully",
                [Keys.RefreshError] = "Failed to refresh wallet overview",
                [Keys.AccountSwitched] = "Switched to account",
                [Keys.SelectAccountFirst] = "Please select an account first",
                [Keys.BalanceLoadError] = "Failed to load balance",
                [Keys.PriceUnavailable] = "Price data unavailable",
                [Keys.ErrorText] = "Error",
                [Keys.NetworkSwitched] = "Switched to network",
                [Keys.LoadingBalance] = "Loading balance...",
                [Keys.LoadingTransactions] = "Loading transactions...",
                [Keys.TransactionHistory] = "History",
                [Keys.TokenBalances] = "Tokens",
                [Keys.PendingTransactions] = "Pending Transactions",
                [Keys.ViewAll] = "View All",
                [Keys.BackButton] = "Back",
                [Keys.NoExplorerConfigured] = "No block explorer configured for this network",
                [Keys.FailedToOpenExplorer] = "Failed to open block explorer",
                [Keys.TransactionHashCopied] = "Transaction hash copied to clipboard"
            });
            
            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.Title] = "Resumen de Cartera",
                [Keys.WalletOverview] = "Resumen de Cartera",
                [Keys.Balance] = "Saldo",
                [Keys.TotalBalance] = "Saldo Total",
                [Keys.RecentTransactions] = "Transacciones Recientes",
                [Keys.NoTransactions] = "No se encontraron transacciones recientes",
                [Keys.SendButton] = "Enviar",
                [Keys.ReceiveButton] = "Recibir",
                [Keys.ManageAccountsButton] = "Gestionar Cuentas",
                [Keys.CopyAddressButton] = "Copiar Dirección",
                [Keys.RefreshButton] = "Actualizar",
                [Keys.NetworkLabel] = "Red",
                [Keys.AccountLabel] = "Cuenta",
                [Keys.AddressCopied] = "Dirección copiada al portapapeles",
                [Keys.RefreshSuccess] = "Resumen de cartera actualizado exitosamente",
                [Keys.RefreshError] = "Error al actualizar el resumen de cartera",
                [Keys.AccountSwitched] = "Cambiado a cuenta",
                [Keys.SelectAccountFirst] = "Por favor selecciona una cuenta primero",
                [Keys.BalanceLoadError] = "Error al cargar el saldo",
                [Keys.PriceUnavailable] = "Datos de precio no disponibles",
                [Keys.ErrorText] = "Error",
                [Keys.NetworkSwitched] = "Cambiado a red",
                [Keys.LoadingBalance] = "Cargando saldo...",
                [Keys.LoadingTransactions] = "Cargando transacciones...",
                [Keys.TransactionHistory] = "Historial",
                [Keys.TokenBalances] = "Tokens",
                [Keys.PendingTransactions] = "Transacciones Pendientes",
                [Keys.ViewAll] = "Ver Todo",
                [Keys.BackButton] = "Atrás",
                [Keys.NoExplorerConfigured] = "No hay explorador de bloques configurado para esta red",
                [Keys.FailedToOpenExplorer] = "Error al abrir el explorador de bloques",
                [Keys.TransactionHashCopied] = "Hash de transacción copiado al portapapeles"
            });
        }
    }
}