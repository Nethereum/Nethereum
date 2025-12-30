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
            public const string VerifiedBalance = "VerifiedBalance";
            public const string VerifiedBalances = "VerifiedBalances";
            public const string FinalizedBlock = "FinalizedBlock";
            public const string LightClientNotConfigured = "LightClientNotConfigured";
            public const string VerificationFailed = "VerificationFailed";
            public const string BalancesMismatchWarning = "BalancesMismatchWarning";
            public const string VerifiedBalanceLoading = "VerifiedBalanceLoading";
            public const string OptimisticBlock = "OptimisticBlock";
            public const string RpcLimitationMessage = "RpcLimitationMessage";
            public const string FinalizedBalance = "FinalizedBalance";
            public const string OptimisticBalance = "OptimisticBalance";
            public const string FinalizedBalanceTooltip = "FinalizedBalanceTooltip";
            public const string OptimisticBalanceTooltip = "OptimisticBalanceTooltip";
            public const string FinalizedUnavailable = "FinalizedUnavailable";
            public const string OptimisticUnavailable = "OptimisticUnavailable";
            public const string BalancesDifferFromRpc = "BalancesDifferFromRpc";
        }

        public WalletOverviewLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.Title] = "Current Account",
                [Keys.WalletOverview] = "Current Account",
                [Keys.Balance] = "Balance",
                [Keys.TotalBalance] = "Total Balance",
                [Keys.RecentTransactions] = "Recent Transactions",
                [Keys.NoTransactions] = "No recent transactions found",
                [Keys.SendButton] = "Send",
                [Keys.ReceiveButton] = "Receive",
                [Keys.ManageAccountsButton] = "Manage Accounts",
                [Keys.CopyAddressButton] = "Copy Address",
                [Keys.RefreshButton] = "Refresh",
                [Keys.NetworkLabel] = "Chain",
                [Keys.AccountLabel] = "Account",
                [Keys.AddressCopied] = "Address copied to clipboard",
                [Keys.RefreshSuccess] = "Account refreshed successfully",
                [Keys.RefreshError] = "Failed to refresh account",
                [Keys.AccountSwitched] = "Switched to account",
                [Keys.SelectAccountFirst] = "Please select an account first",
                [Keys.BalanceLoadError] = "Failed to load balance",
                [Keys.PriceUnavailable] = "Price data unavailable",
                [Keys.ErrorText] = "Error",
                [Keys.NetworkSwitched] = "Switched to chain",
                [Keys.LoadingBalance] = "Loading balance...",
                [Keys.LoadingTransactions] = "Loading transactions...",
                [Keys.TransactionHistory] = "History",
                [Keys.TokenBalances] = "Tokens",
                [Keys.PendingTransactions] = "Pending Transactions",
                [Keys.ViewAll] = "View All",
                [Keys.BackButton] = "Back",
                [Keys.NoExplorerConfigured] = "No block explorer configured for this network",
                [Keys.FailedToOpenExplorer] = "Failed to open block explorer",
                [Keys.TransactionHashCopied] = "Transaction hash copied to clipboard",
                [Keys.VerifiedBalance] = "Verified Balance",
                [Keys.VerifiedBalances] = "Verified Balances",
                [Keys.FinalizedBlock] = "Block",
                [Keys.LightClientNotConfigured] = "Light client not configured",
                [Keys.VerificationFailed] = "Verification failed",
                [Keys.BalancesMismatchWarning] = "Warning: Verified balance differs from RPC balance",
                [Keys.VerifiedBalanceLoading] = "Verifying balance...",
                [Keys.OptimisticBlock] = "Block",
                [Keys.RpcLimitationMessage] = "RPC node does not support historical proofs. Use an archive node for verified balances.",
                [Keys.FinalizedBalance] = "Finalized",
                [Keys.OptimisticBalance] = "Optimistic",
                [Keys.FinalizedBalanceTooltip] = "~12 min behind, never reverts",
                [Keys.OptimisticBalanceTooltip] = "Seconds behind, may revert",
                [Keys.FinalizedUnavailable] = "Unavailable",
                [Keys.OptimisticUnavailable] = "Unavailable",
                [Keys.BalancesDifferFromRpc] = "Differs from RPC"
            });
            
            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.Title] = "Cuenta Activa",
                [Keys.WalletOverview] = "Cuenta Activa",
                [Keys.Balance] = "Saldo",
                [Keys.TotalBalance] = "Saldo Total",
                [Keys.RecentTransactions] = "Transacciones Recientes",
                [Keys.NoTransactions] = "No se encontraron transacciones recientes",
                [Keys.SendButton] = "Enviar",
                [Keys.ReceiveButton] = "Recibir",
                [Keys.ManageAccountsButton] = "Gestionar Cuentas",
                [Keys.CopyAddressButton] = "Copiar Dirección",
                [Keys.RefreshButton] = "Actualizar",
                [Keys.NetworkLabel] = "Cadena",
                [Keys.AccountLabel] = "Cuenta",
                [Keys.AddressCopied] = "Dirección copiada al portapapeles",
                [Keys.RefreshSuccess] = "Cuenta actualizada exitosamente",
                [Keys.RefreshError] = "Error al actualizar la cuenta",
                [Keys.AccountSwitched] = "Cambiado a cuenta",
                [Keys.SelectAccountFirst] = "Por favor selecciona una cuenta primero",
                [Keys.BalanceLoadError] = "Error al cargar el saldo",
                [Keys.PriceUnavailable] = "Datos de precio no disponibles",
                [Keys.ErrorText] = "Error",
                [Keys.NetworkSwitched] = "Cambiado a cadena",
                [Keys.LoadingBalance] = "Cargando saldo...",
                [Keys.LoadingTransactions] = "Cargando transacciones...",
                [Keys.TransactionHistory] = "Historial",
                [Keys.TokenBalances] = "Tokens",
                [Keys.PendingTransactions] = "Transacciones Pendientes",
                [Keys.ViewAll] = "Ver Todo",
                [Keys.BackButton] = "Atrás",
                [Keys.NoExplorerConfigured] = "No hay explorador de bloques configurado para esta red",
                [Keys.FailedToOpenExplorer] = "Error al abrir el explorador de bloques",
                [Keys.TransactionHashCopied] = "Hash de transacción copiado al portapapeles",
                [Keys.VerifiedBalance] = "Saldo Verificado",
                [Keys.VerifiedBalances] = "Saldos Verificados",
                [Keys.FinalizedBlock] = "Bloque",
                [Keys.LightClientNotConfigured] = "Cliente ligero no configurado",
                [Keys.VerificationFailed] = "Verificación fallida",
                [Keys.BalancesMismatchWarning] = "Advertencia: El saldo verificado difiere del saldo RPC",
                [Keys.VerifiedBalanceLoading] = "Verificando saldo...",
                [Keys.OptimisticBlock] = "Bloque",
                [Keys.RpcLimitationMessage] = "El nodo RPC no admite pruebas históricas. Use un nodo de archivo para saldos verificados.",
                [Keys.FinalizedBalance] = "Finalizado",
                [Keys.OptimisticBalance] = "Optimista",
                [Keys.FinalizedBalanceTooltip] = "~12 min atrás, nunca revierte",
                [Keys.OptimisticBalanceTooltip] = "Segundos atrás, puede revertir",
                [Keys.FinalizedUnavailable] = "No disponible",
                [Keys.OptimisticUnavailable] = "No disponible",
                [Keys.BalancesDifferFromRpc] = "Difiere del RPC"
            });
        }
    }
}