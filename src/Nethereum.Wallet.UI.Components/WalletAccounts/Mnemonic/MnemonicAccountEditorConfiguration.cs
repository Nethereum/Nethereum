using Nethereum.Wallet.UI.Components.Core.Configuration;
using System;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.Mnemonic
{
    public class MnemonicAccountEditorConfiguration : BaseWalletConfiguration
    {
        public MnemonicAccountTextConfiguration MnemonicText { get; set; } = new();
        public MnemonicAccountBehaviorConfiguration MnemonicBehavior { get; set; } = new();
        public MnemonicAccountSecurityConfiguration MnemonicSecurity { get; set; } = new();
    }
    public class MnemonicAccountTextConfiguration
    {
        public string AccountNameLabel { get; set; } = "Account Name";
        public string AccountNameHelperText { get; set; } = "Give your account a memorable name (optional)";
        public string AccountNamePlaceholder { get; set; } = "My Wallet";
        
        public string GenerateTabText { get; set; } = "Generate";
        public string ImportTabText { get; set; } = "Import";
        
        public string Generate12WordsText { get; set; } = "Generate 12-Word Phrase";
        public string Generate24WordsText { get; set; } = "Generate 24-Word Phrase";
        public string YourSeedPhraseTitle { get; set; } = "Your Seed Phrase";
        public string WordCountDisplay { get; set; } = "words";
        public string HidePhraseTooltip { get; set; } = "Hide phrase";
        public string ShowPhraseTooltip { get; set; } = "Show phrase";
        public string CopyPhraseTooltip { get; set; } = "Copy entire seed phrase";
        public string CopyAllWordsText { get; set; } = "Copy All Words";
        public string RevealInstructionText { get; set; } = "Click the eye icon to reveal your seed phrase";
        
        public string SecurityGuidelinesTitle { get; set; } = "Security Guidelines";
        public string WriteDownAdvice { get; set; } = "Write down your seed phrase on paper and store it safely";
        public string KeepPrivateAdvice { get; set; } = "Keep it private - never share with anyone";
        public string BackupAdvice { get; set; } = "Consider multiple secure backup locations";
        
        public string BackupWarningTitle { get; set; } = "⚠️ Important: Save Your Seed Phrase";
        public string BackupWarningMessage { get; set; } = "You must save your seed phrase before continuing. Write it down on paper and store it safely. Without it, you cannot recover your wallet.";
        public string BackupConfirmationText { get; set; } = "I Have Safely Saved My Seed Phrase";
        
        public string SeedPhraseLabel { get; set; } = "Seed Phrase";
        public string SeedPhraseHelperText { get; set; } = "Enter your 12 or 24-word seed phrase separated by spaces";
        
        public string PassphraseLabel { get; set; } = "Passphrase (Optional)";
        public string PassphraseHelperText { get; set; } = "Optional passphrase for additional security. Note: not all wallets support passphrases";
        
        public string DerivedAddressLabel { get; set; } = "Derived Address (Account 0):";
        public string CopyAddressTitle { get; set; } = "Copy Address";
        
        public string BackToLoginText { get; set; } = "Back to Login";
        public string BackToAccountSelectionText { get; set; } = "Back to Account Selection";
        public string AddAccountText { get; set; } = "Add Account";
        
        public string ValidMnemonicMessage { get; set; } = "Valid mnemonic phrase";
        public string InvalidMnemonicMessage { get; set; } = "Invalid mnemonic phrase";
        
        public string WeakStrengthText { get; set; } = "Weak";
        public string StrongStrengthText { get; set; } = "Strong";
        public string VeryStrongStrengthText { get; set; } = "Very Strong";
    }
    public class MnemonicAccountBehaviorConfiguration
    {
        public bool ShowBackToLogin { get; set; } = true;
        public bool ShowBackToAccountSelection { get; set; } = true;
        public bool RequireBackupConfirmation { get; set; } = true;
        public bool AutoHideSeedPhrase { get; set; } = true;
        public bool EnableClipboardCopy { get; set; } = true;
        public bool ShowSecurityGuidelines { get; set; } = true;
        public bool AutoFocusAccountName { get; set; } = true;
        public bool ShowAddressPreview { get; set; } = true;
        public bool EnablePassphraseEntry { get; set; } = true;
        public bool ValidateOnInput { get; set; } = true;
        public int DefaultWordCount { get; set; } = 12;
        public bool ShowWordCountButtons { get; set; } = true;
    }
    public class MnemonicAccountSecurityConfiguration
    {
        public bool RequireAccountName { get; set; } = false;
        public int MaxAccountNameLength { get; set; } = 50;
        public bool ValidateMnemonicStrength { get; set; } = true;
        public bool WarnOnWeakMnemonic { get; set; } = true;
        public bool PreventScreenshots { get; set; } = false;
        public int MaxPassphraseLength { get; set; } = 100;
        public bool LogSecurityEvents { get; set; } = true;
    }
    public class MnemonicAccountEditorConfigurationBuilder : BaseWalletConfigurationBuilder<MnemonicAccountEditorConfiguration, MnemonicAccountEditorConfigurationBuilder>
    {
        protected override MnemonicAccountEditorConfigurationBuilder This => this;

        public MnemonicAccountEditorConfigurationBuilder WithAccountNameLabel(string label)
        {
            _config.MnemonicText.AccountNameLabel = label;
            return this;
        }

        public MnemonicAccountEditorConfigurationBuilder WithAccountNameHelperText(string helperText)
        {
            _config.MnemonicText.AccountNameHelperText = helperText;
            return this;
        }

        public MnemonicAccountEditorConfigurationBuilder WithDefaultWordCount(int wordCount)
        {
            _config.MnemonicBehavior.DefaultWordCount = wordCount;
            return this;
        }

        public MnemonicAccountEditorConfigurationBuilder RequireBackupConfirmation(bool require = true)
        {
            _config.MnemonicBehavior.RequireBackupConfirmation = require;
            return this;
        }

        public MnemonicAccountEditorConfigurationBuilder EnablePassphrase(bool enable = true)
        {
            _config.MnemonicBehavior.EnablePassphraseEntry = enable;
            return this;
        }

        public MnemonicAccountEditorConfigurationBuilder ShowNavigationButtons(bool showBackToLogin = true, bool showBackToAccountSelection = true)
        {
            _config.MnemonicBehavior.ShowBackToLogin = showBackToLogin;
            _config.MnemonicBehavior.ShowBackToAccountSelection = showBackToAccountSelection;
            return this;
        }

        public MnemonicAccountEditorConfigurationBuilder RequireAccountName(bool require = true)
        {
            _config.MnemonicSecurity.RequireAccountName = require;
            return this;
        }

        public MnemonicAccountEditorConfigurationBuilder ConfigureMnemonicText(Action<MnemonicAccountTextConfiguration> configure)
        {
            configure(_config.MnemonicText);
            return this;
        }

        public MnemonicAccountEditorConfigurationBuilder ConfigureMnemonicBehavior(Action<MnemonicAccountBehaviorConfiguration> configure)
        {
            configure(_config.MnemonicBehavior);
            return this;
        }

        public MnemonicAccountEditorConfigurationBuilder ConfigureMnemonicSecurity(Action<MnemonicAccountSecurityConfiguration> configure)
        {
            configure(_config.MnemonicSecurity);
            return this;
        }
    }
}