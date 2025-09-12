using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Transactions
{
    public class TransactionHistoryLocalizer : ComponentLocalizerBase<TransactionHistoryViewModel>
    {
        public static class Keys
        {
            public const string PendingTab = "PendingTab";
            public const string RecentTab = "RecentTab";
            public const string AllTab = "AllTab";
            
            public const string Pending = "Pending";
            public const string Mining = "Mining";
            public const string Confirmed = "Confirmed";
            public const string Failed = "Failed";
            public const string Dropped = "Dropped";
            
            public const string Description = "Description";
            public const string Hash = "Hash";
            public const string TransactionHash = "TransactionHash";
            public const string Status = "Status";
            public const string Type = "Type";
            public const string Time = "Time";
            public const string Confirmations = "Confirmations";
            public const string Actions = "Actions";
            public const string Network = "Network";
            public const string Value = "Value";
            public const string GasUsed = "GasUsed";
            public const string GasPrice = "GasPrice";
            public const string BlockNumber = "BlockNumber";
            public const string Nonce = "Nonce";
            public const string From = "From";
            public const string To = "To";
            
            public const string Retry = "Retry";
            public const string ViewOnExplorer = "ViewOnExplorer";
            public const string CopyHash = "CopyHash";
            public const string ShowDetails = "ShowDetails";
            public const string Cancel = "Cancel";
            public const string Close = "Close";
            
            public const string NoPendingTransactions = "NoPendingTransactions";
            public const string NoPendingDescription = "NoPendingDescription";
            public const string NoRecentTransactions = "NoRecentTransactions";
            public const string NoRecentDescription = "NoRecentDescription";
            public const string LoadingTransactions = "LoadingTransactions";
            public const string HashCopied = "HashCopied";
            public const string TransactionConfirmed = "TransactionConfirmed";
            public const string TransactionFailed = "TransactionFailed";
            public const string HasBeenConfirmed = "HasBeenConfirmed";
            public const string HasFailed = "HasFailed";
            
            public const string RetryConfirmTitle = "RetryConfirmTitle";
            public const string RetryConfirmMessage = "RetryConfirmMessage";
            public const string RetryFailed = "RetryFailed";
            public const string RetrySuccess = "RetrySuccess";
            public const string RetrySubmitted = "RetrySubmitted";
            
            public const string TransactionDetails = "TransactionDetails";
            public const string ErrorMessage = "ErrorMessage";
            public const string TimeElapsed = "TimeElapsed";
            public const string SubmittedAt = "SubmittedAt";
            public const string ConfirmedAt = "ConfirmedAt";
            
            public const string FilterPlaceholder = "FilterPlaceholder";
            public const string ClearFilter = "ClearFilter";
            
            public const string Title = "Title";
            public const string Subtitle = "Subtitle";
            
            public const string ViewAll = "ViewAll";
            
            public const string HideDetails = "HideDetails";
            public const string LoadingBlockchainData = "LoadingBlockchainData";
            public const string ReceiptDetails = "ReceiptDetails";
            public const string BlockHash = "BlockHash";
            public const string TransactionIndex = "TransactionIndex";
            public const string EffectiveGasPrice = "EffectiveGasPrice";
            public const string CumulativeGasUsed = "CumulativeGasUsed";
            public const string ReceiptStatus = "ReceiptStatus";
            public const string ContractCreated = "ContractCreated";
            public const string EventLogs = "EventLogs";
            public const string InputData = "InputData";
            public const string MaxFeePerGas = "MaxFeePerGas";
            public const string MaxPriorityFeePerGas = "MaxPriorityFeePerGas";
        }
        
        public TransactionHistoryLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }
        
        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.Title] = "Transaction History",
                [Keys.Subtitle] = "Monitor and manage your transactions",
                
                [Keys.PendingTab] = "Pending",
                [Keys.RecentTab] = "Recent",
                [Keys.AllTab] = "All Transactions",
                
                [Keys.Pending] = "Pending",
                [Keys.Mining] = "Mining",
                [Keys.Confirmed] = "Confirmed",
                [Keys.Failed] = "Failed",
                [Keys.Dropped] = "Dropped",
                
                [Keys.Description] = "Description",
                [Keys.Hash] = "Transaction Hash",
                [Keys.TransactionHash] = "Transaction Hash",
                [Keys.Status] = "Status",
                [Keys.Type] = "Type",
                [Keys.Time] = "Time",
                [Keys.Confirmations] = "Confirmations",
                [Keys.Actions] = "Actions",
                [Keys.Network] = "Network",
                [Keys.Value] = "Value",
                [Keys.GasUsed] = "Gas Used",
                [Keys.GasPrice] = "Gas Price",
                [Keys.BlockNumber] = "Block Number",
                [Keys.Nonce] = "Nonce",
                [Keys.From] = "From",
                [Keys.To] = "To",
                
                [Keys.Retry] = "Retry",
                [Keys.ViewOnExplorer] = "View on Explorer",
                [Keys.CopyHash] = "Copy Hash",
                [Keys.ShowDetails] = "Details",
                [Keys.Cancel] = "Cancel",
                [Keys.Close] = "Close",
                
                [Keys.NoPendingTransactions] = "No pending transactions",
                [Keys.NoPendingDescription] = "Your pending transactions will appear here",
                [Keys.NoRecentTransactions] = "No recent transactions",
                [Keys.NoRecentDescription] = "Your completed transactions will appear here",
                [Keys.LoadingTransactions] = "Loading transactions...",
                [Keys.HashCopied] = "Transaction hash copied to clipboard",
                [Keys.TransactionConfirmed] = "Transaction Confirmed",
                [Keys.TransactionFailed] = "Transaction Failed",
                [Keys.HasBeenConfirmed] = "has been confirmed",
                [Keys.HasFailed] = "has failed",
                
                [Keys.RetryConfirmTitle] = "Retry Transaction",
                [Keys.RetryConfirmMessage] = "Do you want to retry this failed transaction with updated gas settings?",
                [Keys.RetryFailed] = "Failed to retry transaction",
                [Keys.RetrySuccess] = "Transaction Retry Submitted",
                [Keys.RetrySubmitted] = "Your transaction has been resubmitted with updated settings",
                
                [Keys.TransactionDetails] = "Transaction Details",
                [Keys.ErrorMessage] = "Error Message",
                [Keys.TimeElapsed] = "Time Elapsed",
                [Keys.SubmittedAt] = "Submitted At",
                [Keys.ConfirmedAt] = "Confirmed At",
                
                [Keys.FilterPlaceholder] = "Filter by hash or description...",
                [Keys.ClearFilter] = "Clear",
                
                [Keys.ViewAll] = "View All",
                
                [Keys.HideDetails] = "Hide Details",
                [Keys.LoadingBlockchainData] = "Loading blockchain data...",
                [Keys.ReceiptDetails] = "Receipt Details",
                [Keys.BlockHash] = "Block Hash",
                [Keys.TransactionIndex] = "Transaction Index",
                [Keys.EffectiveGasPrice] = "Effective Gas Price",
                [Keys.CumulativeGasUsed] = "Cumulative Gas Used",
                [Keys.ReceiptStatus] = "Receipt Status",
                [Keys.ContractCreated] = "Contract Created",
                [Keys.EventLogs] = "Event Logs",
                [Keys.InputData] = "Input Data",
                [Keys.MaxFeePerGas] = "Max Fee Per Gas",
                [Keys.MaxPriorityFeePerGas] = "Max Priority Fee Per Gas"
            });
            
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.Title] = "Historial de Transacciones",
                [Keys.Subtitle] = "Monitorea y gestiona tus transacciones",
                
                [Keys.PendingTab] = "Pendientes",
                [Keys.RecentTab] = "Recientes",
                [Keys.AllTab] = "Todas las Transacciones",
                
                [Keys.Pending] = "Pendiente",
                [Keys.Mining] = "Minando",
                [Keys.Confirmed] = "Confirmada",
                [Keys.Failed] = "Fallida",
                [Keys.Dropped] = "Descartada",
                
                [Keys.Description] = "Descripción",
                [Keys.Hash] = "Hash de Transacción",
                [Keys.TransactionHash] = "Hash de Transacción",
                [Keys.Status] = "Estado",
                [Keys.Type] = "Tipo",
                [Keys.Time] = "Tiempo",
                [Keys.Confirmations] = "Confirmaciones",
                [Keys.Actions] = "Acciones",
                [Keys.Network] = "Red",
                [Keys.Value] = "Valor",
                [Keys.GasUsed] = "Gas Usado",
                [Keys.GasPrice] = "Precio del Gas",
                [Keys.BlockNumber] = "Número de Bloque",
                [Keys.Nonce] = "Nonce",
                [Keys.From] = "Desde",
                [Keys.To] = "Para",
                
                [Keys.Retry] = "Reintentar",
                [Keys.ViewOnExplorer] = "Ver en Explorador",
                [Keys.CopyHash] = "Copiar Hash",
                [Keys.ShowDetails] = "Detalles",
                [Keys.Cancel] = "Cancelar",
                [Keys.Close] = "Cerrar",
                
                [Keys.NoPendingTransactions] = "No hay transacciones pendientes",
                [Keys.NoPendingDescription] = "Sus transacciones pendientes aparecerán aquí",
                [Keys.NoRecentTransactions] = "No hay transacciones recientes",
                [Keys.NoRecentDescription] = "Sus transacciones completadas aparecerán aquí",
                [Keys.LoadingTransactions] = "Cargando transacciones...",
                [Keys.HashCopied] = "Hash de transacción copiado al portapapeles",
                [Keys.TransactionConfirmed] = "Transacción Confirmada",
                [Keys.TransactionFailed] = "Transacción Fallida",
                [Keys.HasBeenConfirmed] = "ha sido confirmada",
                [Keys.HasFailed] = "ha fallado",
                
                [Keys.RetryConfirmTitle] = "Reintentar Transacción",
                [Keys.RetryConfirmMessage] = "¿Deseas reintentar esta transacción fallida con configuración de gas actualizada?",
                [Keys.RetryFailed] = "Error al reintentar la transacción",
                [Keys.RetrySuccess] = "Reintento de Transacción Enviado",
                [Keys.RetrySubmitted] = "Tu transacción ha sido reenviada con configuración actualizada",
                
                [Keys.TransactionDetails] = "Detalles de la Transacción",
                [Keys.ErrorMessage] = "Mensaje de Error",
                [Keys.TimeElapsed] = "Tiempo Transcurrido",
                [Keys.SubmittedAt] = "Enviada a las",
                [Keys.ConfirmedAt] = "Confirmada a las",
                
                [Keys.FilterPlaceholder] = "Filtrar por hash o descripción...",
                [Keys.ClearFilter] = "Limpiar",
                
                [Keys.ViewAll] = "Ver Todo",
                
                [Keys.HideDetails] = "Ocultar detalles",
                [Keys.LoadingBlockchainData] = "Cargando datos de blockchain...",
                [Keys.ReceiptDetails] = "Detalles del recibo",
                [Keys.BlockHash] = "Hash del bloque",
                [Keys.TransactionIndex] = "Índice de transacción",
                [Keys.EffectiveGasPrice] = "Precio de gas efectivo",
                [Keys.CumulativeGasUsed] = "Gas acumulado usado",
                [Keys.ReceiptStatus] = "Estado del recibo",
                [Keys.ContractCreated] = "Contrato creado",
                [Keys.EventLogs] = "Registros de eventos",
                [Keys.InputData] = "Datos de entrada",
                [Keys.MaxFeePerGas] = "Tarifa máxima por gas",
                [Keys.MaxPriorityFeePerGas] = "Tarifa de prioridad máxima por gas"
            });
        }
    }
}