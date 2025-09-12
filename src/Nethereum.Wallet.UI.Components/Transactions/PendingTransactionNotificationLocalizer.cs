using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Transactions
{
    public class PendingTransactionNotificationLocalizer : ComponentLocalizerBase<PendingTransactionNotificationService>
    {
        public static class Keys
        {
            public const string TransactionSubmitted = "TransactionSubmitted";
            public const string TransactionMining = "TransactionMining";
            public const string TransactionConfirmed = "TransactionConfirmed";
            public const string TransactionFailed = "TransactionFailed";
            public const string TransactionReorg = "TransactionReorg";
            
            public const string TransactionSubmittedWithHash = "TransactionSubmittedWithHash";
            public const string TransactionMiningWithHash = "TransactionMiningWithHash";
            public const string TransactionConfirmedWithHash = "TransactionConfirmedWithHash";
            public const string TransactionFailedWithHash = "TransactionFailedWithHash";
            public const string TransactionReorgWithHash = "TransactionReorgWithHash";
            
            public const string ConfirmationCount = "ConfirmationCount";
            public const string TransactionFailedGeneric = "TransactionFailedGeneric";
            public const string UnknownTransaction = "UnknownTransaction";
            
            public const string ViewDetails = "ViewDetails";
            public const string ViewOnExplorer = "ViewOnExplorer";
            
            public const string SendTransaction = "SendTransaction";
            public const string TokenTransfer = "TokenTransfer";
            public const string ContractInteraction = "ContractInteraction";
            public const string ContractDeployment = "ContractDeployment";
        }
        
        public PendingTransactionNotificationLocalizer(IWalletLocalizationService globalService) 
            : base(globalService)
        {
        }
        
        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.TransactionSubmitted] = "{0} has been submitted to the network",
                [Keys.TransactionMining] = "{0} is being mined",
                [Keys.TransactionConfirmed] = "{0} has been confirmed",
                [Keys.TransactionFailed] = "{0} has failed",
                [Keys.TransactionReorg] = "{0} may have been affected by a chain reorganization",
                
                [Keys.TransactionSubmittedWithHash] = "{0} submitted ({1})",
                [Keys.TransactionMiningWithHash] = "{0} mining ({1})",
                [Keys.TransactionConfirmedWithHash] = "{0} confirmed ({1})",
                [Keys.TransactionFailedWithHash] = "{0} failed ({1})",
                [Keys.TransactionReorgWithHash] = "{0} reorg detected ({1})",
                
                [Keys.ConfirmationCount] = "({0} confirmations)",
                [Keys.TransactionFailedGeneric] = "Please check the transaction details for more information",
                [Keys.UnknownTransaction] = "Transaction",
                
                [Keys.ViewDetails] = "View",
                [Keys.ViewOnExplorer] = "Explorer",
                
                [Keys.SendTransaction] = "Transfer",
                [Keys.TokenTransfer] = "Token transfer",
                [Keys.ContractInteraction] = "Contract interaction",
                [Keys.ContractDeployment] = "Contract deployment"
            });
            
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.TransactionSubmitted] = "{0} ha sido enviada a la red",
                [Keys.TransactionMining] = "{0} está siendo minada",
                [Keys.TransactionConfirmed] = "{0} ha sido confirmada",
                [Keys.TransactionFailed] = "{0} ha fallado",
                [Keys.TransactionReorg] = "{0} puede haber sido afectada por una reorganización de la cadena",
                
                [Keys.TransactionSubmittedWithHash] = "{0} enviada ({1})",
                [Keys.TransactionMiningWithHash] = "{0} minando ({1})",
                [Keys.TransactionConfirmedWithHash] = "{0} confirmada ({1})",
                [Keys.TransactionFailedWithHash] = "{0} falló ({1})",
                [Keys.TransactionReorgWithHash] = "{0} reorganización detectada ({1})",
                
                [Keys.ConfirmationCount] = "({0} confirmaciones)",
                [Keys.TransactionFailedGeneric] = "Por favor revise los detalles de la transacción para más información",
                [Keys.UnknownTransaction] = "Transacción",
                
                [Keys.ViewDetails] = "Ver",
                [Keys.ViewOnExplorer] = "Explorador",
                
                [Keys.SendTransaction] = "Transferencia",
                [Keys.TokenTransfer] = "Transferencia de token",
                [Keys.ContractInteraction] = "Interacción con contrato",
                [Keys.ContractDeployment] = "Despliegue de contrato"
            });
        }
    }
}