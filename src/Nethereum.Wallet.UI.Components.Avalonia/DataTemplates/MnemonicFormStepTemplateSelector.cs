using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Nethereum.Wallet.UI.Components.Avalonia.Views.WalletAccounts.Mnemonic;
using System;

namespace Nethereum.Wallet.UI.Components.Avalonia.DataTemplates
{
    public class MnemonicFormStepTemplateSelector : IDataTemplate
    {
        public IDataTemplate SetupTemplate { get; set; }
        public IDataTemplate MnemonicTemplate { get; set; }
        public IDataTemplate SecurityTemplate { get; set; }

        public bool Match(object? data)
        {
            return data is MnemonicAccountCreation.FormStep;
        }

        public Control? Build(object? data)
        {
            if (data is MnemonicAccountCreation.FormStep step)
            {
                var template = step switch
                {
                    MnemonicAccountCreation.FormStep.Setup => SetupTemplate,
                    MnemonicAccountCreation.FormStep.Mnemonic => MnemonicTemplate,
                    MnemonicAccountCreation.FormStep.Security => SecurityTemplate,
                    _ => null
                };
                return template?.Build(data);
            }
            return null;
        }
    }
}
