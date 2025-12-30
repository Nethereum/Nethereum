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
            public const string SeedPhrase = "SeedPhrase";
            public const string ManageSubtitle = "ManageSubtitle";
            public const string NoSeedPhrases = "NoSeedPhrases";
            public const string NoSeedPhrasesMessage = "NoSeedPhrasesMessage";
            public const string AddSeedPhrase = "AddSeedPhrase";
            public const string SearchSeedPhrases = "SearchSeedPhrases";
            public const string LoadingSeedPhrases = "LoadingSeedPhrases";
            public const string Refresh = "Refresh";
            public const string Delete = "Delete";
            public const string DeleteSeedPhraseTooltip = "DeleteSeedPhraseTooltip";
            public const string Protected = "Protected";
            public const string Account = "Account";
            public const string Accounts = "Accounts";
            public const string Error = "Error";
            public const string Success = "Success";
            public const string NoVaultAvailable = "NoVaultAvailable";
            public const string CannotDeleteMnemonicWithAccounts = "CannotDeleteMnemonicWithAccounts";
        }

        public static readonly Dictionary<string, string> DefaultValues = new()
        {
            { Keys.MnemonicManager, "Seed Phrase Manager" },
            { Keys.SeedPhrases, "Seed Phrases" },
            { Keys.SeedPhrase, "Seed Phrase" },
            { Keys.ManageSubtitle, "Manage your seed phrases and derived accounts" },
            { Keys.NoSeedPhrases, "No Seed Phrases" },
            { Keys.NoSeedPhrasesMessage, "You haven't added any seed phrases yet. Add one to create derived accounts." },
            { Keys.AddSeedPhrase, "Add Seed Phrase" },
            { Keys.SearchSeedPhrases, "Search seed phrases..." },
            { Keys.LoadingSeedPhrases, "Loading seed phrases..." },
            { Keys.Refresh, "Refresh" },
            { Keys.Delete, "Delete" },
            { Keys.DeleteSeedPhraseTooltip, "Delete seed phrase" },
            { Keys.Protected, "Protected" },
            { Keys.Account, "account" },
            { Keys.Accounts, "accounts" },
            { Keys.Error, "Error" },
            { Keys.Success, "Success" },
            { Keys.NoVaultAvailable, "No vault available" },
            { Keys.CannotDeleteMnemonicWithAccounts, "Cannot delete mnemonic that has associated accounts. Remove all accounts first." }
        };

        public static readonly Dictionary<string, string> SpanishValues = new()
        {
            { Keys.MnemonicManager, "Administrador de Frases Semilla" },
            { Keys.SeedPhrases, "Frases Semilla" },
            { Keys.SeedPhrase, "Frase Semilla" },
            { Keys.ManageSubtitle, "Administra tus frases semilla y cuentas derivadas" },
            { Keys.NoSeedPhrases, "Sin Frases Semilla" },
            { Keys.NoSeedPhrasesMessage, "Aún no has añadido ninguna frase semilla. Añade una para crear cuentas derivadas." },
            { Keys.AddSeedPhrase, "Añadir Frase Semilla" },
            { Keys.SearchSeedPhrases, "Buscar frases semilla..." },
            { Keys.LoadingSeedPhrases, "Cargando frases semilla..." },
            { Keys.Refresh, "Actualizar" },
            { Keys.Delete, "Eliminar" },
            { Keys.DeleteSeedPhraseTooltip, "Eliminar frase semilla" },
            { Keys.Protected, "Protegida" },
            { Keys.Account, "cuenta" },
            { Keys.Accounts, "cuentas" },
            { Keys.Error, "Error" },
            { Keys.Success, "Éxito" },
            { Keys.NoVaultAvailable, "No hay bóveda disponible" },
            { Keys.CannotDeleteMnemonicWithAccounts, "No se puede eliminar la frase semilla que tiene cuentas asociadas. Elimine todas las cuentas primero." }
        };

        public MnemonicListViewModelLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", DefaultValues);
            _globalService.RegisterTranslations(_componentName, "es-ES", SpanishValues);
        }
    }
}