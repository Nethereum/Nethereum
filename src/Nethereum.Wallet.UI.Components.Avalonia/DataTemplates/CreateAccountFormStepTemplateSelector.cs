using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Nethereum.Wallet.UI.Components.CreateAccount;
using Nethereum.Wallet.UI.Components.WalletAccounts;
using System;

namespace Nethereum.Wallet.UI.Components.Avalonia.DataTemplates
{
    public class CreateAccountFormStepTemplateSelector : IDataTemplate
    {
        public IDataTemplate AccountTypeSelectionTemplate { get; set; }
        public IDataTemplate MnemonicAccountCreationTemplate { get; set; }
        public IDataTemplate PrivateKeyAccountCreationTemplate { get; set; }
        public IDataTemplate ViewOnlyAccountCreationTemplate { get; set; }

        public bool Match(object? data)
        {
            return data is IAccountCreationViewModel || data == null;
        }

        public Control? Build(object? data)
        {
            if (data is IAccountCreationViewModel accountCreationViewModel)
            {
                var template = accountCreationViewModel.GetType().Name switch
                {
                    "MnemonicAccountCreationViewModel" => MnemonicAccountCreationTemplate,
                    "PrivateKeyAccountCreationViewModel" => PrivateKeyAccountCreationTemplate,
                    "ViewOnlyAccountCreationViewModel" => ViewOnlyAccountCreationTemplate,
                    _ => AccountTypeSelectionTemplate // Default to account type selection
                };
                return template?.Build(data);
            }
            return AccountTypeSelectionTemplate?.Build(data); // Default when no account type is selected
        }
    }
}
