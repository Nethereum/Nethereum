using Microsoft.Extensions.DependencyInjection;
using Nethereum.Wallet;
using Nethereum.Wallet.UI.Components.AccountList;
using Nethereum.Wallet.UI.Components.Avalonia.Extensions;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Dashboard;
using Nethereum.Wallet.UI.Components.WalletAccounts.Mnemonic;
using Xunit;

namespace Nethereum.Wallet.UI.Components.Avalonia.UnitTests
{
    public class AvaloniaLegacySeedWarningTests
    {
        [Fact]
        public void Given_an_import_phrase_with_true_or_false_in_any_case_When_reading_the_hint_flag_Then_it_is_set_without_blocking_the_import()
        {
            var viewModel = new MnemonicAccountCreationViewModel(
                new DefaultComponentLocalizer<MnemonicAccountCreationViewModel>(null),
                new InMemoryWalletVaultService(),
                null);
            viewModel.IsGenerateMode = false;

            viewModel.Mnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";
            Assert.False(viewModel.ShowLegacyWordsHint);
            Assert.True(viewModel.IsValidMnemonic);

            viewModel.Mnemonic = "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon FALSE";
            Assert.True(viewModel.ShowLegacyWordsHint);

            viewModel.Mnemonic = "true abandon abandon";
            Assert.True(viewModel.ShowLegacyWordsHint);

            viewModel.Mnemonic = "";
            Assert.False(viewModel.ShowLegacyWordsHint);
        }

        [Fact]
        public void Given_the_avalonia_components_When_resolving_the_legacy_seed_texts_Then_english_and_spanish_translations_exist()
        {
            var services = new ServiceCollection();
            services.AddNethereumWalletAvaloniaComponents();
            using var provider = services.BuildServiceProvider();
            var localization = provider.GetRequiredService<IWalletLocalizationService>();

            new WalletDashboardLocalizer(localization);
            new AccountListLocalizer(localization);
            new MnemonicAccountEditorLocalizer(localization);

            foreach (var language in new[] { "en-US", "es-ES" })
            {
                Assert.NotEqual(WalletDashboardLocalizer.Keys.LegacySeedWarning,
                    localization.GetTranslation(typeof(WalletDashboardViewModel).FullName, language, WalletDashboardLocalizer.Keys.LegacySeedWarning));
                Assert.NotEqual(AccountListLocalizer.Keys.LegacySeedMarker,
                    localization.GetTranslation(typeof(AccountListViewModel).FullName, language, AccountListLocalizer.Keys.LegacySeedMarker));
                Assert.NotEqual(MnemonicAccountEditorLocalizer.Keys.LegacyWordsHint,
                    localization.GetTranslation(typeof(MnemonicAccountCreationViewModel).FullName, language, MnemonicAccountEditorLocalizer.Keys.LegacyWordsHint));
            }
        }
    }
}
