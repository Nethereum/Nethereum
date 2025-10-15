using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Wallet.UI.Components.Core.Registry;

namespace Nethereum.Wallet.UI.Components.Dashboard
{
    public class StaticDashboardPluginRegistry : IDashboardPluginRegistry
    {
        private readonly IComponentRegistry _componentRegistry;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public StaticDashboardPluginRegistry(IComponentRegistry componentRegistry,
                                             IServiceScopeFactory serviceScopeFactory)
        {
            _componentRegistry = componentRegistry;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public IEnumerable<IDashboardPluginViewModel> GetAvailablePlugins()
        {
            using var scope = _serviceScopeFactory.CreateScope();
            return scope.ServiceProvider
                        .GetServices<IDashboardPluginViewModel>()
                        .Where(vm => vm.IsVisible && vm.IsEnabled && vm.IsAvailable())
                        .OrderBy(vm => vm.SortOrder)
                        .ToList();
        }

        public IDashboardPluginViewModel? GetPlugin(string pluginId)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            return scope.ServiceProvider
                        .GetServices<IDashboardPluginViewModel>()
                        .FirstOrDefault(vm => vm.PluginId == pluginId);
        }

        public Type? GetComponentType(IDashboardPluginViewModel viewModel)
            => _componentRegistry.GetComponentType(viewModel.GetType());

        public Type? GetComponentType<TViewModel>() where TViewModel : class, IDashboardPluginViewModel
            => _componentRegistry.GetComponentType<TViewModel>();
    }
}
