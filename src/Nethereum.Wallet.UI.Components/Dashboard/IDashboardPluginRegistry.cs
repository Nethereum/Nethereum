using System;
using System.Collections.Generic;

namespace Nethereum.Wallet.UI.Components.Dashboard
{
    public interface IDashboardPluginRegistry
    {
        IEnumerable<IDashboardPluginViewModel> GetAvailablePlugins();
        IDashboardPluginViewModel? GetPlugin(string pluginId);
        Type? GetComponentType(IDashboardPluginViewModel viewModel);
        Type? GetComponentType<TViewModel>() where TViewModel : class, IDashboardPluginViewModel;
    }
}