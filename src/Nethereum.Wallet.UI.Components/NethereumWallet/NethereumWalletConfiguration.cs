using Nethereum.Wallet.UI.Components.Core.Configuration;
using Nethereum.Wallet.UI.Components.Core.Localization;
using System;

namespace Nethereum.Wallet.UI.Components.NethereumWallet
{
    public class NethereumWalletConfiguration : BaseWalletConfiguration, IComponentConfiguration
    {
        public new string ComponentId { get; set; } = "NethereumWallet";

        public bool ShowProgressIndicators { get; set; } = true;
        public bool EnableKeyboardShortcuts { get; set; } = true;
        public int PasswordMinimumStrength { get; set; } = 1;
        public bool AllowPasswordVisibilityToggle { get; set; } = true;
        public bool ShowPasswordStrengthIndicator { get; set; } = true;
    }
    public class NethereumWalletConfigurationBuilder : BaseWalletConfigurationBuilder<NethereumWalletConfiguration, NethereumWalletConfigurationBuilder>
    {
        protected override NethereumWalletConfigurationBuilder This => this;

        public NethereumWalletConfigurationBuilder EnableProgressIndicators(bool enable = true)
        {
            _config.ShowProgressIndicators = enable;
            return this;
        }

        public NethereumWalletConfigurationBuilder EnableKeyboardShortcuts(bool enable = true)
        {
            _config.EnableKeyboardShortcuts = enable;
            return this;
        }

        public NethereumWalletConfigurationBuilder WithPasswordMinimumStrength(int strength)
        {
            _config.PasswordMinimumStrength = Math.Max(1, Math.Min(5, strength));
            return this;
        }

        public NethereumWalletConfigurationBuilder AllowPasswordVisibilityToggle(bool allow = true)
        {
            _config.AllowPasswordVisibilityToggle = allow;
            return this;
        }

        public NethereumWalletConfigurationBuilder ShowPasswordStrengthIndicator(bool show = true)
        {
            _config.ShowPasswordStrengthIndicator = show;
            return this;
        }
    }
}