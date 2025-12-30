using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.Mnemonic
{
    public class MnemonicDetailsViewModelLocalizer : ComponentLocalizerBase<MnemonicDetailsViewModel>
    {
        public MnemonicDetailsViewModelLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", MnemonicDetailsLocalizer.DefaultValues);
            _globalService.RegisterTranslations(_componentName, "es-ES", MnemonicDetailsLocalizer.SpanishValues);
        }
    }
}