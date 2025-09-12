using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Wallet.UI.Components.Dashboard.Services
{
    public interface IDashboardNavigationService
    {
        Task NavigateToPluginAsync(string pluginId, Dictionary<string, object>? parameters = null);
        Task NavigateCurrentPluginAsync(Dictionary<string, object> parameters);
        void RegisterActivePlugin(string pluginId, object? pluginComponent);
        event NavigationRequestedHandler? NavigationRequested;
    }
    public class DashboardNavigationEventArgs
    {
        public string PluginId { get; set; }
        public Dictionary<string, object>? Parameters { get; set; }

        public DashboardNavigationEventArgs(string pluginId, Dictionary<string, object>? parameters = null)
        {
            PluginId = pluginId;
            Parameters = parameters;
        }
    }

    public delegate Task NavigationRequestedHandler(object sender, DashboardNavigationEventArgs e);
}