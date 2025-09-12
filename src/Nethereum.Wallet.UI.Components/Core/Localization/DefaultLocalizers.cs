using Nethereum.Wallet.UI.Components.NethereumWallet;
using Nethereum.Wallet.UI.Components.WalletAccounts.Mnemonic;
using Nethereum.Wallet.UI.Components.WalletAccounts.PrivateKey;
using Nethereum.Wallet.UI.Components.WalletAccounts.ViewOnly;
using Nethereum.Wallet.UI.Components.WalletAccounts.SmartContract;
using Nethereum.Wallet.UI.Components.AccountList;
using Nethereum.Wallet.UI.Components.Dialogs.Localization;
using Nethereum.Wallet.UI.Components.Networks;

namespace Nethereum.Wallet.UI.Components.Core.Localization
{
    public static class DefaultLocalizers
    {
        public static void RegisterAll(IWalletLocalizationService localizationService)
        {
            localizationService.RegisterLocalizer(new NethereumWalletLocalizer(localizationService));
            
            localizationService.RegisterLocalizer(new PrivateKeyAccountEditorLocalizer(localizationService));
            localizationService.RegisterLocalizer(new MnemonicAccountEditorLocalizer(localizationService));
            localizationService.RegisterLocalizer(new ViewOnlyAccountEditorLocalizer(localizationService));
            localizationService.RegisterLocalizer(new SmartContractAccountEditorLocalizer(localizationService));
            localizationService.RegisterLocalizer(new VaultMnemonicAccountEditorLocalizer(localizationService));
            
            localizationService.RegisterLocalizer(new AccountManagementLocalizer(localizationService));
            
            localizationService.RegisterLocalizer(new NetworkListLocalizer(localizationService));
            localizationService.RegisterLocalizer(new NetworkDetailsLocalizer(localizationService));
            localizationService.RegisterLocalizer(new NetworkManagementPluginLocalizer(localizationService));
            
            localizationService.RegisterLocalizer(new PasswordConfirmationDialogLocalizer(localizationService));
            localizationService.RegisterLocalizer(new DialogLocalizer(localizationService));
        }
    }
}