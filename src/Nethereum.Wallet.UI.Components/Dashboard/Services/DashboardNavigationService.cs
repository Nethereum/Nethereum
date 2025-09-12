using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Dashboard.Services
{
    public class DashboardNavigationService : IDashboardNavigationService
    {
        private string? _activePluginId;
        private object? _activePluginComponent;

        public event NavigationRequestedHandler? NavigationRequested;

        public async Task NavigateToPluginAsync(string pluginId, Dictionary<string, object>? parameters = null)
        {
            // If navigating to the same plugin and it implements INavigatablePlugin, call directly
            if (_activePluginId == pluginId && 
                _activePluginComponent is INavigatablePlugin navigatablePlugin)
            {
                await navigatablePlugin.NavigateWithParametersAsync(parameters ?? new Dictionary<string, object>());
                return;
            }

            if (NavigationRequested != null)
            {
                var args = new DashboardNavigationEventArgs(pluginId, parameters);
                await NavigationRequested.Invoke(this, args);
            }
        }

        public async Task NavigateCurrentPluginAsync(Dictionary<string, object> parameters)
        {
            if (_activePluginComponent is INavigatablePlugin navigatablePlugin)
            {
                await navigatablePlugin.NavigateWithParametersAsync(parameters);
            }
        }

        public void RegisterActivePlugin(string pluginId, object? pluginComponent)
        {
            _activePluginId = pluginId;
            _activePluginComponent = pluginComponent;
        }
    }
}