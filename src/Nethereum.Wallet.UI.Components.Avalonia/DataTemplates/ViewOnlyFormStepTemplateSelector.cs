using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Nethereum.Wallet.UI.Components.Avalonia.Views.WalletAccounts.ViewOnly;
using System;

namespace Nethereum.Wallet.UI.Components.Avalonia.DataTemplates
{
    public class ViewOnlyFormStepTemplateSelector : IDataTemplate
    {
        public IDataTemplate SetupTemplate { get; set; }
        public IDataTemplate AddressTemplate { get; set; }
        public IDataTemplate ConfirmTemplate { get; set; }

        public bool Match(object? data)
        {
            return data is ViewOnlyAccountCreation.FormStep;
        }

        public Control? Build(object? data)
        {
            if (data is ViewOnlyAccountCreation.FormStep step)
            {
                var template = step switch
                {
                    ViewOnlyAccountCreation.FormStep.Setup => SetupTemplate,
                    ViewOnlyAccountCreation.FormStep.Address => AddressTemplate,
                    ViewOnlyAccountCreation.FormStep.Confirm => ConfirmTemplate,
                    _ => null
                };
                return template?.Build(data);
            }
            return null;
        }
    }
}
