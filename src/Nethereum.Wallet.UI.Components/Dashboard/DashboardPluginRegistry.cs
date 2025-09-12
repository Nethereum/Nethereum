using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Wallet.UI.Components.Core.Registry;

namespace Nethereum.Wallet.UI.Components.Dashboard
{
    public class DashboardPluginRegistry : IDashboardPluginRegistry
    {
        private readonly IComponentRegistry _componentRegistry;
        private readonly IServiceProvider _serviceProvider;

        public DashboardPluginRegistry(
            IComponentRegistry componentRegistry, 
            IServiceProvider serviceProvider)
        {
            _componentRegistry = componentRegistry;
            _serviceProvider = serviceProvider;
        }

        public IEnumerable<IDashboardPluginViewModel> GetAvailablePlugins()
        {
            // Get all IDashboardPluginViewModel instances from DI, filter by visibility and availability, ordered by SortOrder
            return _serviceProvider.GetServices<IDashboardPluginViewModel>()
                                  .Where(vm => vm.IsVisible && vm.IsEnabled && vm.IsAvailable())
                                  .OrderBy(vm => vm.SortOrder);
        }

        public IDashboardPluginViewModel? GetPlugin(string pluginId)
        {
            return _serviceProvider.GetServices<IDashboardPluginViewModel>()
                                  .FirstOrDefault(vm => vm.PluginId == pluginId);
        }

        public Type? GetComponentType(IDashboardPluginViewModel viewModel)
        {
            return _componentRegistry.GetComponentType(viewModel.GetType());
        }

        public Type? GetComponentType<TViewModel>() where TViewModel : class, IDashboardPluginViewModel
        {
            return _componentRegistry.GetComponentType<TViewModel>();
        }
    }
}