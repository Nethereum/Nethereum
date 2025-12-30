using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Dashboard;

namespace Nethereum.Wallet.UI.Components.Contacts
{
    public class ContactListPluginViewModel : IDashboardPluginViewModel
    {
        private readonly IComponentLocalizer<ContactListViewModel> _localizer;

        public ContactListPluginViewModel(IComponentLocalizer<ContactListViewModel> localizer)
        {
            _localizer = localizer;
        }

        public string PluginId => "contacts";

        public string DisplayName => _localizer.GetString(ContactListLocalizer.Keys.PluginName);

        public string Description => _localizer.GetString(ContactListLocalizer.Keys.PluginDescription);

        public string Icon => "contacts";

        public int SortOrder => 50;

        public bool IsVisible => true;

        public bool IsEnabled => true;

        public bool IsAvailable() => true;
    }
}
