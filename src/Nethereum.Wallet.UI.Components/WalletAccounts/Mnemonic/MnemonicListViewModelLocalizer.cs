using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.Mnemonic
{
    public class MnemonicListViewModelLocalizer : ComponentLocalizerBase<MnemonicListViewModel>
    {
        public static class Keys
        {
            public const string MnemonicManager = "MnemonicManager";
            public const string SeedPhrases = "SeedPhrases";
            public const string NoSeedPhrases = "NoSeedPhrases";
            public const string AddSeedPhrase = "AddSeedPhrase";
            public const string SearchSeedPhrases = "SearchSeedPhrases";
            public const string LoadingSeedPhrases = "LoadingSeedPhrases";
            public const string Refresh = "Refresh";
            public const string Delete = "Delete";
            public const string Protected = "Protected";
            public const string Account = "Account";
            public const string Accounts = "Accounts";
            public const string Error = "Error";
            public const string Success = "Success";
        }

        public static readonly Dictionary<string, string> DefaultValues = new()
        {
            { Keys.MnemonicManager, "Seed Phrase Manager" },
            { Keys.SeedPhrases, "Seed Phrases" },
            { Keys.NoSeedPhrases, "No Seed Phrases" },
            { Keys.AddSeedPhrase, "Add Seed Phrase" },
            { Keys.SearchSeedPhrases, "Search seed phrases..." },
            { Keys.LoadingSeedPhrases, "Loading seed phrases..." },
            { Keys.Refresh, "Refresh" },
            { Keys.Delete, "Delete" },
            { Keys.Protected, "Protected" },
            { Keys.Account, "account" },
            { Keys.Accounts, "accounts" },
            { Keys.Error, "Error" },
            { Keys.Success, "Success" }
        };

        public MnemonicListViewModelLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", DefaultValues);
        }
    }
}