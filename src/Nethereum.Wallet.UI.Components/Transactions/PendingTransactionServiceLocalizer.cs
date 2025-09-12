using Nethereum.Wallet.Services.Transactions;
using Nethereum.Wallet.UI.Components.Core.Localization;
using System.Collections.Generic;

namespace Nethereum.Wallet.UI.Components.Transactions
{
    public class PendingTransactionServiceLocalizer : ComponentLocalizerBase<PendingTransactionService>
    {
        public static class Keys
        {
            public const string NativeTokenTransfer = "NativeTokenTransfer";
            public const string GeneralTransaction = "GeneralTransaction";
        }
        
        public PendingTransactionServiceLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }
        
        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.NativeTokenTransfer] = "Native Token Transfer",
                [Keys.GeneralTransaction] = "Transaction"
            });
            
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.NativeTokenTransfer] = "Transferencia de Token Nativo",
                [Keys.GeneralTransaction] = "Transacción"
            });
        }
    }
}