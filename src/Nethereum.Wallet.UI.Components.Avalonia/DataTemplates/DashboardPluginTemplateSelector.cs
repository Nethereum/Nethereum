using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Nethereum.Wallet.UI.Components.Dashboard;
using System;

namespace Nethereum.Wallet.UI.Components.Avalonia.DataTemplates
{
    public class DashboardPluginTemplateSelector : IDataTemplate
    {
        public IDataTemplate AccountListTemplate { get; set; }
        public IDataTemplate CreateAccountTemplate { get; set; }
        // Add more templates for other plugins as needed

        public bool Match(object? data)
        {
            return data is IDashboardPluginViewModel;
        }

        public Control? Build(object? data)
        {
            if (data is IDashboardPluginViewModel plugin)
            {
                var template = plugin.PluginId switch
                {
                    "account-list" => AccountListTemplate,
                    "create-account" => CreateAccountTemplate,
                    // Add more cases for other plugins
                    _ => null
                };
                return template?.Build(data);
            }
            return null;
        }
    }
}
