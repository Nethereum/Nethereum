using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Nethereum.Wallet.UI.Components.Avalonia.Views.WalletAccounts.PrivateKey;
using System;

namespace Nethereum.Wallet.UI.Components.Avalonia.DataTemplates
{
    public class PrivateKeyFormStepTemplateSelector : IDataTemplate
    {
        public IDataTemplate SetupTemplate { get; set; }
        public IDataTemplate PrivateKeyTemplate { get; set; }
        public IDataTemplate ConfirmTemplate { get; set; }

        public bool Match(object? data)
        {
            return data is PrivateKeyAccountCreation.FormStep;
        }

        public Control? Build(object? data)
        {
            if (data is PrivateKeyAccountCreation.FormStep step)
            {
                var template = step switch
                {
                    PrivateKeyAccountCreation.FormStep.Setup => SetupTemplate,
                    PrivateKeyAccountCreation.FormStep.PrivateKey => PrivateKeyTemplate,
                    PrivateKeyAccountCreation.FormStep.Confirm => ConfirmTemplate,
                    _ => null
                };
                return template?.Build(data);
            }
            return null;
        }
    }
}
