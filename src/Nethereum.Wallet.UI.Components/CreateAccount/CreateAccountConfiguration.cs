using Nethereum.Wallet.UI.Components.Core.Configuration;

namespace Nethereum.Wallet.UI.Components.CreateAccount
{
    public class CreateAccountConfiguration : BaseWalletConfiguration, IComponentConfiguration
    {
        public new string ComponentId { get; set; } = "CreateAccount";
        public bool ShowAccountTypeDescriptions { get; set; } = true;
        public bool AutoSelectNewAccount { get; set; } = true;
    }
}