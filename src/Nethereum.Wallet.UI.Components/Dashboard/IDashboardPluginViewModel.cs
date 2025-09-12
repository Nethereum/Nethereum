using System;

namespace Nethereum.Wallet.UI.Components.Dashboard
{
    public interface IDashboardPluginViewModel
    {
        string PluginId { get; }
        string DisplayName { get; }
        string Description { get; }
        string Icon { get; }
        int SortOrder { get; }
        bool IsVisible { get; }
        bool IsEnabled { get; }
        bool IsAvailable();
    }
}