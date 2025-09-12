using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.SendTransaction
{
    public class SendNativeTokenLocalizer : ComponentLocalizerBase<SendNativeTokenViewModel>
    {
        public static class Keys
        {
            public const string Title = "Title";
            public const string Cancel = "Cancel";
            public const string Previous = "Previous";
            public const string ContinueButton = "ContinueButton";
            public const string ConfirmTransaction = "ConfirmTransaction";
            public const string LoadingTransaction = "LoadingTransaction";
            public const string PreparingTransaction = "PreparingTransaction";
            
            public const string Step1Title = "Step1Title";
            public const string Step2Title = "Step2Title";
            public const string Step3Title = "Step3Title";
            
            public const string TransactionSent = "TransactionSent";
            public const string TransactionFailed = "TransactionFailed";
            public const string TransactionHash = "TransactionHash";
            public const string ErrorMessage = "ErrorMessage";
            public const string ViewTransactionButton = "ViewTransactionButton";
            
            public const string NextStep = "NextStep";
            public const string PreviousStep = "PreviousStep";
            public const string SendTransaction = "SendTransaction";
            public const string SimulateTransaction = "SimulateTransaction";
            public const string SetMaxAmount = "SetMaxAmount";
            public const string Reset = "Reset";
            
            public const string RecipientDetails = "RecipientDetails";
            public const string TransactionDetails = "TransactionDetails";
            public const string Confirmation = "Confirmation";
            
            public const string EnterRecipientAndAmount = "EnterRecipientAndAmount";
            public const string ReviewGasAndFees = "ReviewGasAndFees";
            public const string ConfirmAndSend = "ConfirmAndSend";
            
            public const string RecipientSection = "RecipientSection";
            public const string RecipientAddress = "RecipientAddress";
            public const string RecipientAddressPlaceholder = "RecipientAddressPlaceholder";
            public const string Amount = "Amount";
            public const string AmountPlaceholder = "AmountPlaceholder";
            public const string AvailableBalance = "AvailableBalance";
            public const string MaxButton = "MaxButton";
            public const string PleaseCorrectErrors = "PleaseCorrectErrors";
            public const string Done = "Done";
        }
        
        public SendNativeTokenLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }
        
        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.Title] = "Send Native Token",
                [Keys.Cancel] = "Cancel",
                [Keys.Previous] = "Back",
                [Keys.ContinueButton] = "Continue",
                [Keys.ConfirmTransaction] = "Confirm Transaction",
                [Keys.LoadingTransaction] = "Sending transaction...",
                [Keys.PreparingTransaction] = "Preparing transaction...",
                
                [Keys.Step1Title] = "Recipient Details",
                [Keys.Step2Title] = "Transaction Details", 
                [Keys.Step3Title] = "Confirmation",
                
                [Keys.TransactionSent] = "Transaction sent successfully",
                [Keys.TransactionFailed] = "Transaction failed",
                [Keys.TransactionHash] = "Transaction Hash",
                [Keys.ErrorMessage] = "Error Message", 
                [Keys.ViewTransactionButton] = "View on Explorer",
                
                [Keys.NextStep] = "Next",
                [Keys.PreviousStep] = "Back",
                [Keys.SendTransaction] = "Send Transaction",
                [Keys.SimulateTransaction] = "Simulate",
                [Keys.SetMaxAmount] = "Max",
                [Keys.Reset] = "Reset",
                
                [Keys.RecipientDetails] = "Recipient Details",
                [Keys.TransactionDetails] = "Transaction Details",
                [Keys.Confirmation] = "Confirmation",
                
                [Keys.EnterRecipientAndAmount] = "Enter the recipient address and amount to send",
                [Keys.ReviewGasAndFees] = "Review gas settings and transaction fees",
                [Keys.ConfirmAndSend] = "Confirm transaction details and send",
                
                [Keys.RecipientSection] = "Recipient Details",
                [Keys.RecipientAddress] = "Recipient Address",
                [Keys.RecipientAddressPlaceholder] = "0x...",
                [Keys.Amount] = "Amount",
                [Keys.AmountPlaceholder] = "0.0",
                [Keys.AvailableBalance] = "Available Balance",
                [Keys.MaxButton] = "Max",
                [Keys.PleaseCorrectErrors] = "Please correct validation errors",
                [Keys.Done] = "Done"
            });
            
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.Title] = "Enviar Token Nativo",
                [Keys.Cancel] = "Cancelar",
                [Keys.Previous] = "Atrás",
                [Keys.ContinueButton] = "Continuar",
                [Keys.ConfirmTransaction] = "Confirmar Transacción",
                [Keys.LoadingTransaction] = "Enviando transacción...",
                [Keys.PreparingTransaction] = "Preparando transacción...",
                
                [Keys.Step1Title] = "Detalles del Destinatario",
                [Keys.Step2Title] = "Detalles de la Transacción",
                [Keys.Step3Title] = "Confirmación",
                
                [Keys.TransactionSent] = "Transacción enviada exitosamente",
                [Keys.TransactionFailed] = "Transacción fallida",
                [Keys.TransactionHash] = "Hash de Transacción",
                [Keys.ErrorMessage] = "Mensaje de Error",
                [Keys.ViewTransactionButton] = "Ver en Explorer",
                
                [Keys.NextStep] = "Siguiente",
                [Keys.PreviousStep] = "Atrás", 
                [Keys.SendTransaction] = "Enviar Transacción",
                [Keys.SimulateTransaction] = "Simular",
                [Keys.SetMaxAmount] = "Máx",
                [Keys.Reset] = "Reiniciar",
                
                [Keys.RecipientDetails] = "Detalles del Destinatario",
                [Keys.TransactionDetails] = "Detalles de la Transacción",
                [Keys.Confirmation] = "Confirmación",
                
                [Keys.EnterRecipientAndAmount] = "Ingrese la dirección del destinatario y la cantidad a enviar",
                [Keys.ReviewGasAndFees] = "Revisar configuración de gas y tarifas de transacción",
                [Keys.ConfirmAndSend] = "Confirmar detalles de la transacción y enviar",
                
                [Keys.RecipientSection] = "Detalles del Destinatario",
                [Keys.RecipientAddress] = "Dirección del Destinatario",
                [Keys.RecipientAddressPlaceholder] = "0x...",
                [Keys.Amount] = "Cantidad",
                [Keys.AmountPlaceholder] = "0.0",
                [Keys.AvailableBalance] = "Saldo Disponible",
                [Keys.MaxButton] = "Máx",
                [Keys.PleaseCorrectErrors] = "Por favor corrija los errores de validación",
                [Keys.Done] = "Hecho"
            });
        }
    }
}