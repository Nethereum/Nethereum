using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.SendTransaction.Components
{
    public class TransactionStatusLocalizer : ComponentLocalizerBase<TransactionStatusViewModel>
    {
        public static class Keys
        {
            public const string TransactionSent = "TransactionSent";
            public const string TransactionFailed = "TransactionFailed";
            public const string TransactionSubmitted = "TransactionSubmitted";
            public const string TransactionConfirmed = "TransactionConfirmed";
            public const string TransactionFailedMessage = "TransactionFailedMessage";
            
            public const string StatusPending = "StatusPending";
            public const string StatusMining = "StatusMining";
            public const string StatusConfirmed = "StatusConfirmed";
            public const string StatusFailed = "StatusFailed";
            public const string StatusProcessing = "StatusProcessing";
            
            public const string TransactionStatus = "TransactionStatus";
            public const string TransactionDetails = "TransactionDetails";
            
            public const string TransactionHash = "TransactionHash";
            public const string Status = "Status";
            public const string Confirmations = "Confirmations";
            public const string GasUsed = "GasUsed";
            public const string TransactionCost = "TransactionCost";
            public const string From = "From";
            public const string To = "To";
            public const string Value = "Value";
            public const string Network = "Network";
            
            public const string WaitingForConfirmation = "WaitingForConfirmation";
            public const string ConfirmationsProgress = "ConfirmationsProgress";
            
            public const string ViewOnExplorer = "ViewOnExplorer";
            public const string ViewHistory = "ViewHistory";
            public const string NewTransaction = "NewTransaction";
            public const string CopyHash = "CopyHash";
            public const string Close = "Close";
        }
        
        public TransactionStatusLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }
        
        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.TransactionSent] = "Transaction Sent",
                [Keys.TransactionFailed] = "Transaction Failed",
                [Keys.TransactionSubmitted] = "Transaction submitted successfully",
                [Keys.TransactionConfirmed] = "Transaction confirmed",
                [Keys.TransactionFailedMessage] = "Transaction execution failed",
                
                [Keys.StatusPending] = "Transaction Pending",
                [Keys.StatusMining] = "Transaction Mining",
                [Keys.StatusConfirmed] = "Transaction Confirmed",
                [Keys.StatusFailed] = "Transaction Failed",
                [Keys.StatusProcessing] = "Processing Transaction",
                
                [Keys.TransactionStatus] = "Transaction Status",
                [Keys.TransactionDetails] = "Transaction Details",
                
                [Keys.TransactionHash] = "Transaction Hash",
                [Keys.Status] = "Status",
                [Keys.Confirmations] = "Confirmations",
                [Keys.GasUsed] = "Gas Used",
                [Keys.TransactionCost] = "Transaction Cost",
                [Keys.From] = "From",
                [Keys.To] = "To",
                [Keys.Value] = "Value",
                [Keys.Network] = "Network",
                
                [Keys.WaitingForConfirmation] = "Waiting for confirmation...",
                [Keys.ConfirmationsProgress] = "Confirmations: {0}/12",
                
                [Keys.ViewOnExplorer] = "View on Explorer",
                [Keys.ViewHistory] = "View History",
                [Keys.NewTransaction] = "New Transaction",
                [Keys.CopyHash] = "Copy Hash",
                [Keys.Close] = "Close"
            });
            
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.TransactionSent] = "Transacción Enviada",
                [Keys.TransactionFailed] = "Transacción Fallida",
                [Keys.TransactionSubmitted] = "Transacción enviada exitosamente",
                [Keys.TransactionConfirmed] = "Transacción confirmada",
                [Keys.TransactionFailedMessage] = "La ejecución de la transacción falló",
                
                [Keys.StatusPending] = "Transacción Pendiente",
                [Keys.StatusMining] = "Transacción Minando",
                [Keys.StatusConfirmed] = "Transacción Confirmada",
                [Keys.StatusFailed] = "Transacción Fallida",
                [Keys.StatusProcessing] = "Procesando Transacción",
                
                [Keys.TransactionStatus] = "Estado de Transacción",
                [Keys.TransactionDetails] = "Detalles de la Transacción",
                
                [Keys.TransactionHash] = "Hash de Transacción",
                [Keys.Status] = "Estado",
                [Keys.Confirmations] = "Confirmaciones",
                [Keys.GasUsed] = "Gas Usado",
                [Keys.TransactionCost] = "Costo de Transacción",
                [Keys.From] = "Desde",
                [Keys.To] = "Para",
                [Keys.Value] = "Valor",
                [Keys.Network] = "Red",
                
                [Keys.WaitingForConfirmation] = "Esperando confirmación...",
                [Keys.ConfirmationsProgress] = "Confirmaciones: {0}/12",
                
                [Keys.ViewOnExplorer] = "Ver en Explorador",
                [Keys.ViewHistory] = "Ver Historial",
                [Keys.NewTransaction] = "Nueva Transacción",
                [Keys.CopyHash] = "Copiar Hash",
                [Keys.Close] = "Cerrar"
            });
        }
    }
}