using Nethereum.Wallet.UI.Components.Core.Configuration;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.Mnemonic
{
    public class MnemonicAccountDetailsConfiguration : IComponentConfiguration
    {
        public string ComponentId => "mnemonic-account-details";
        public string ComponentName => "MnemonicAccountDetails";
        public string ComponentVersion => "1.0.0";
        public string ComponentDescription => "Detailed view for mnemonic-derived accounts with general settings and security operations";
        public bool ShowDerivationPath { get; set; } = true;
        public bool ShowAccountIndex { get; set; } = true;
        public bool AllowPrivateKeyReveal { get; set; } = true;
        public bool ShowAdvancedInfo { get; set; } = false;
        public int MaxAccountNameLength { get; set; } = 50;
        public bool AllowAccountNameEditing { get; set; } = true;
    }
}