using System;

namespace Nethereum.Wallet.UI.Components.Core.Configuration
{
    public abstract class BaseWalletConfiguration : IComponentConfiguration
    {
        public string ComponentId { get; set; } = Guid.NewGuid().ToString();
        public WalletFlowMode FlowMode { get; set; } = WalletFlowMode.Simple;
        public WalletTextConfiguration Text { get; set; } = new();
        public WalletBehaviorConfiguration Behavior { get; set; } = new();
        public WalletSecurityConfiguration Security { get; set; } = new();
        public virtual string GetAccountTypeDisplayName(string accountType)
        {
            return accountType switch
            {
                "Mnemonic" => "HD Wallet",
                "PrivateKey" => "Private Key",
                "ViewOnly" => "View Only",
                "SmartContract" => "Smart Contract",
                _ => accountType
            };
        }
        public virtual string GetAccountTypeDescription(string accountType)
        {
            return accountType switch
            {
                "Mnemonic" => "Hierarchical Deterministic wallet using mnemonic phrase",
                "PrivateKey" => "Account imported from private key",
                "ViewOnly" => "Watch-only account for viewing balances",
                "SmartContract" => "Account Abstraction smart contract wallet",
                _ => "Custom account type"
            };
        }
    }
    public enum WalletFlowMode
    {
        Simple,
        Advanced,
        Custom
    }
    public class WalletTextConfiguration
    {
        public string LoginTitle { get; set; } = "Welcome Back";
        public string LoginSubtitle { get; set; } = "Enter your password to unlock your wallet";
        public string LoginButtonText { get; set; } = "Unlock Wallet";
        public string CreateTitle { get; set; } = "Create New Wallet";
        public string CreateSubtitle { get; set; } = "Set up a new wallet vault to securely store your accounts";
        public string CreateButtonText { get; set; } = "Create Wallet";
        public string ConnectWalletTitle { get; set; } = "Connect Your Wallet";
        public string ConnectMessage { get; set; } = "Get started by connecting or creating your wallet";
        public string ConnectButtonText { get; set; } = "Connect Wallet";
        public string PasswordLabel { get; set; } = "Password";
        public string CreatePasswordLabel { get; set; } = "Create Password";
        public string ConfirmPasswordLabel { get; set; } = "Confirm Password";
        public string PasswordHelperText { get; set; } = "Choose a strong password to protect your wallet";
        public string LoadingText { get; set; } = "Setting up wallet...";
        public string ConnectingText { get; set; } = "Connecting...";
        public string ProcessingText { get; set; } = "Processing...";
        public string PasswordRequiredError { get; set; } = "Password is required";
        public string PasswordMismatchError { get; set; } = "Passwords do not match";
        public string InvalidPasswordError { get; set; } = "Invalid password. Please try again.";
        public string LoginFailedError { get; set; } = "Login failed. Please check your password and try again.";
        public string CreateFailedError { get; set; } = "Failed to create wallet. Please try again.";
        public string ResetWalletText { get; set; } = "Reset Wallet";
        public string ResetConfirmMessage { get; set; } = "Are you sure you want to reset your wallet? This will permanently delete all accounts and data. This action cannot be undone.";
        public string ResetSuccessMessage { get; set; } = "Wallet has been reset. You can now create a new wallet.";
    }
    public class WalletBehaviorConfiguration
    {
        public bool EnableWalletReset { get; set; } = false;
        public bool AutoFocusPasswordField { get; set; } = true;
        public bool ShowPasswordStrengthIndicator { get; set; } = true;
        public bool EnablePasswordVisibilityToggle { get; set; } = true;
        public bool AutoSaveProgress { get; set; } = true;
        public int AutoSaveIntervalSeconds { get; set; } = 30;
        public bool ValidatePasswordStrength { get; set; } = true;
        public bool EnableFormValidation { get; set; } = true;
        public bool ShowLoadingIndicators { get; set; } = true;
        public int OperationTimeoutSeconds { get; set; } = 60;
    }
    public class WalletSecurityConfiguration
    {
        public int MinPasswordLength { get; set; } = 8;
        public int MaxPasswordLength { get; set; } = 128;
        public bool RequireUppercasePassword { get; set; } = true;
        public bool RequireLowercasePassword { get; set; } = true;
        public bool RequireNumericPassword { get; set; } = true;
        public bool RequireSpecialCharacterPassword { get; set; } = true;
        public bool EnableRateLimiting { get; set; } = false;
        public int MaxLoginAttempts { get; set; } = 5;
        public int RateLimitWindowMinutes { get; set; } = 15;
        public bool EnableSessionTimeout { get; set; } = true;
        public int SessionTimeoutMinutes { get; set; } = 30;
        public bool RequirePasswordConfirmation { get; set; } = true;
        public bool EnableSecurityLogging { get; set; } = true;
    }
    public abstract class BaseWalletConfigurationBuilder<TConfiguration, TBuilder>
        where TConfiguration : BaseWalletConfiguration, new()
        where TBuilder : BaseWalletConfigurationBuilder<TConfiguration, TBuilder>
    {
        protected readonly TConfiguration _config = new();

        protected abstract TBuilder This { get; }

        public TBuilder UseSimpleFlow()
        {
            _config.FlowMode = WalletFlowMode.Simple;
            return This;
        }

        public TBuilder UseAdvancedFlow()
        {
            _config.FlowMode = WalletFlowMode.Advanced;
            return This;
        }

        public TBuilder UseCustomFlow()
        {
            _config.FlowMode = WalletFlowMode.Custom;
            return This;
        }

        public TBuilder WithTitle(string title)
        {
            _config.Text.LoginTitle = title;
            _config.Text.ConnectWalletTitle = title;
            return This;
        }

        public TBuilder WithSubtitle(string subtitle)
        {
            _config.Text.LoginSubtitle = subtitle;
            _config.Text.ConnectMessage = subtitle;
            return This;
        }

        public TBuilder EnableWalletReset(bool enable = true)
        {
            _config.Behavior.EnableWalletReset = enable;
            return This;
        }

        public TBuilder WithMinPasswordLength(int length)
        {
            _config.Security.MinPasswordLength = length;
            return This;
        }

        public TBuilder ConfigureText(Action<WalletTextConfiguration> configure)
        {
            configure(_config.Text);
            return This;
        }

        public TBuilder ConfigureBehavior(Action<WalletBehaviorConfiguration> configure)
        {
            configure(_config.Behavior);
            return This;
        }

        public TBuilder ConfigureSecurity(Action<WalletSecurityConfiguration> configure)
        {
            configure(_config.Security);
            return This;
        }

        public TConfiguration Build() => _config;
    }

}