using Nethereum.Wallet.UI.Components.Core.Configuration;

namespace Nethereum.Wallet.UI.Components.AccountList
{
    public class AccountListConfiguration : BaseWalletConfiguration, IComponentConfiguration
    {
        public new string ComponentId { get; set; } = "AccountList";
        public bool ShowBalances { get; set; } = true;
        public bool AllowAccountDeletion { get; set; } = true;
        public bool AllowAccountEditing { get; set; } = true;
        public int AccountsPerPage { get; set; } = 10;
    }
}