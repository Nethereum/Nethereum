using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Nethereum.Wallet.UI.Components.NethereumWallet;
using System;

namespace Nethereum.Wallet.UI.Components.Avalonia.DataTemplates
{
    public class WalletStateTemplateSelector : IDataTemplate
    {
        public IDataTemplate LoginTemplate { get; set; }
        public IDataTemplate CreateWalletTemplate { get; set; }
        public IDataTemplate DashboardTemplate { get; set; }

        public bool Match(object? data)
        {
            return data is NethereumWalletViewModel;
        }

        public Control? Build(object? data)
        {
            if (data is NethereumWalletViewModel viewModel)
            {
                IDataTemplate? template = null;

                if (viewModel.IsBusy)
                {
                    return null; // Loading overlay is handled separately
                }
                else if (viewModel.VaultExists && !viewModel.IsWalletUnlocked)
                {
                    template = LoginTemplate;
                }
                else if (viewModel.IsWalletUnlocked && !viewModel.HasAccounts)
                {
                    template = CreateWalletTemplate;
                }
                else if (viewModel.IsWalletUnlocked && viewModel.HasAccounts)
                {
                    template = DashboardTemplate;
                }
                else // !viewModel.VaultExists
                {
                    template = CreateWalletTemplate; // Initial wallet creation form
                }

                return template?.Build(data);
            }
            return null;
        }
    }
}
