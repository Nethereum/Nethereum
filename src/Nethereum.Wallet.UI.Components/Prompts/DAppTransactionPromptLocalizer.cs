using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.SendTransaction;
using System.Collections.Generic;

namespace Nethereum.Wallet.UI.Components.Prompts
{
    public class DAppTransactionPromptLocalizer : ComponentLocalizerBase<DAppTransactionPromptViewModel>
    {
        public DAppTransactionPromptLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }
        
        public static class Keys
        {
            public const string TransactionRequest = "TransactionRequest";
            public const string ReviewTransaction = "ReviewTransaction";
            public const string ConfirmTransaction = "ConfirmTransaction";
            public const string TransactionStatus = "TransactionStatus";
            public const string RequestDetails = "RequestDetails";
            public const string RequestFrom = "RequestFrom";
            public const string StepReview = "StepReview";
            public const string StepConfirm = "StepConfirm";
            public const string StepStatus = "StepStatus";
            public const string SendTransaction = "SendTransaction";
            public const string Continue = "Continue";
            public const string Back = "Back";
            public const string Reject = "Reject";
            public const string Approve = "Approve";
            public const string Processing = "Processing";
            public const string TransactionSent = "TransactionSent";
            public const string TransactionFailed = "TransactionFailed";
            public const string TransactionFailedToSend = "TransactionFailedToSend";
            public const string TransactionFailedWithReason = "TransactionFailedWithReason";
            public const string Retry = "Retry";
            public const string Close = "Close";
            public const string GasConfiguration = "GasConfiguration";
            public const string EstimatedCost = "EstimatedCost";
            public const string TransactionDetails = "TransactionDetails";
            public const string ReviewGasSettings = "ReviewGasSettings";
            public const string ApproveAndSend = "ApproveAndSend";
            public const string SendingTransaction = "SendingTransaction";
            public const string Cancel = "Cancel";
            public const string RequestFromLabel = "RequestFromLabel";
            public const string Done = "Done";
            public const string RetryTransaction = "RetryTransaction";
        }
        
        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.TransactionRequest] = "Transaction Request",
                [Keys.ReviewTransaction] = "Review Transaction",
                [Keys.ConfirmTransaction] = "Confirm Transaction",
                [Keys.TransactionStatus] = "Transaction Status",
                [Keys.RequestDetails] = "Request Details",
                [Keys.RequestFrom] = "Request from {0}",
                [Keys.StepReview] = "Review",
                [Keys.StepConfirm] = "Confirm",
                [Keys.StepStatus] = "Status",
                [Keys.SendTransaction] = "Send Transaction",
                [Keys.Continue] = "Continue",
                [Keys.Back] = "Back",
                [Keys.Reject] = "Reject",
                [Keys.Approve] = "Approve",
                [Keys.Processing] = "Processing transaction...",
                [Keys.TransactionSent] = "Transaction sent successfully",
                [Keys.TransactionFailed] = "Transaction failed",
                [Keys.TransactionFailedToSend] = "Transaction failed to send.",
                [Keys.TransactionFailedWithReason] = "Transaction failed: {0}",
                [Keys.Retry] = "Retry",
                [Keys.Close] = "Close",
                [Keys.GasConfiguration] = "Gas Configuration",
                [Keys.EstimatedCost] = "Estimated Cost",
                [Keys.TransactionDetails] = "Transaction Details",
                [Keys.ReviewGasSettings] = "Review Gas Settings",
                [Keys.ApproveAndSend] = "Approve & Send",
                [Keys.SendingTransaction] = "Sending transaction...",
                [Keys.Cancel] = "Cancel",
                [Keys.RequestFromLabel] = "Request from",
                [Keys.Done] = "Done",
                [Keys.RetryTransaction] = "Retry Transaction"
            });
            
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.TransactionRequest] = "Solicitud de Transacción",
                [Keys.ReviewTransaction] = "Revisar Transacción",
                [Keys.ConfirmTransaction] = "Confirmar Transacción",
                [Keys.TransactionStatus] = "Estado de Transacción",
                [Keys.RequestDetails] = "Detalles de Solicitud",
                [Keys.RequestFrom] = "Solicitud de {0}",
                [Keys.StepReview] = "Revisar",
                [Keys.StepConfirm] = "Confirmar",
                [Keys.StepStatus] = "Estado",
                [Keys.SendTransaction] = "Enviar Transacción",
                [Keys.Continue] = "Continuar",
                [Keys.Back] = "Atrás",
                [Keys.Reject] = "Rechazar",
                [Keys.Approve] = "Aprobar",
                [Keys.Processing] = "Procesando transacción...",
                [Keys.TransactionSent] = "Transacción enviada exitosamente",
                [Keys.TransactionFailed] = "Transacción fallida",
                [Keys.TransactionFailedToSend] = "La transacción no se pudo enviar.",
                [Keys.TransactionFailedWithReason] = "Transacción fallida: {0}",
                [Keys.Retry] = "Reintentar",
                [Keys.Close] = "Cerrar",
                [Keys.GasConfiguration] = "Configuración de Gas",
                [Keys.EstimatedCost] = "Costo Estimado",
                [Keys.TransactionDetails] = "Detalles de Transacción",
                [Keys.ReviewGasSettings] = "Revisar Configuración de Gas",
                [Keys.ApproveAndSend] = "Aprobar y Enviar",
                [Keys.SendingTransaction] = "Enviando transacción...",
                [Keys.Cancel] = "Cancelar",
                [Keys.RequestFromLabel] = "Solicitud de",
                [Keys.Done] = "Hecho",
                [Keys.RetryTransaction] = "Reintentar Transacción"
            });
        }
    }
}
