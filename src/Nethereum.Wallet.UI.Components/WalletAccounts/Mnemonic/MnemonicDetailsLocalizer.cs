using System.Collections.Generic;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.Mnemonic
{
    public static class MnemonicDetailsLocalizer
    {
        public static class Keys
        {
            public const string MnemonicDetails = "MnemonicDetails";
            public const string Overview = "Overview";
            public const string Security = "Security";
            public const string Accounts = "Accounts";
            public const string Settings = "Settings";

            public const string MnemonicOverview = "MnemonicOverview";
            public const string MnemonicName = "MnemonicName";
            public const string MnemonicType = "MnemonicType";
            public const string TypeLabel = "TypeLabel";
            public const string SecurityLabel = "SecurityLabel";
            public const string CreatedDate = "CreatedDate";
            public const string AccountCount = "AccountCount";
            public const string HasPassphrase = "HasPassphrase";
            public const string NoPassphrase = "NoPassphrase";

            public const string AssociatedAccounts = "AssociatedAccounts";
            public const string NoAccountsMessage = "NoAccountsMessage";
            public const string CreateFirstAccount = "CreateFirstAccount";
            public const string AccountName = "AccountName";
            public const string AccountIndex = "AccountIndex";
            public const string DerivationPath = "DerivationPath";
            public const string Address = "Address";

            public const string RevealSeedPhrase = "RevealSeedPhrase";
            public const string SecurityWarning = "SecurityWarning";
            public const string SecurityWarningMessage = "SecurityWarningMessage";
            public const string SeedPhraseTitle = "SeedPhraseTitle";
            public const string KeepSecure = "KeepSecure";
            public const string KeepSecureMessage = "KeepSecureMessage";
            public const string HideSeedPhrase = "HideSeedPhrase";

            public const string MnemonicLabel = "MnemonicLabel";
            public const string MnemonicLabelPlaceholder = "MnemonicLabelPlaceholder";
            public const string MnemonicLabelHelperText = "MnemonicLabelHelperText";
            public const string SaveLabel = "SaveLabel";
            public const string DeleteMnemonic = "DeleteMnemonic";
            public const string DeleteWarning = "DeleteWarning";
            public const string DeleteWarningMessage = "DeleteWarningMessage";
            public const string DeleteWarningWithAccountsMessage = "DeleteWarningWithAccountsMessage";
            public const string CannotDeleteWithAccounts = "CannotDeleteWithAccounts";

            public const string EditLabel = "EditLabel";
            public const string ViewSeedPhrase = "ViewSeedPhrase";
            public const string ManageAccounts = "ManageAccounts";
            public const string AddAccount = "AddAccount";
            public const string ViewAccount = "ViewAccount";
            public const string Back = "Back";
            public const string Continue = "Continue";
            public const string Save = "Save";
            public const string Cancel = "Cancel";
            public const string Delete = "Delete";
            public const string Confirm = "Confirm";

            public const string Loading = "Loading";
            public const string LoadingMnemonic = "LoadingMnemonic";
            public const string Error = "Error";
            public const string Success = "Success";
            public const string MnemonicNotFound = "MnemonicNotFound";
            public const string MnemonicNotFoundMessage = "MnemonicNotFoundMessage";
            public const string NoVaultAvailable = "NoVaultAvailable";
            public const string LabelUpdated = "LabelUpdated";
            public const string MnemonicDeleted = "MnemonicDeleted";
            public const string PasswordLabel = "PasswordLabel";
            public const string InvalidPassword = "InvalidPassword";

            public const string CopyToClipboard = "CopyToClipboard";
            public const string AddressCopied = "AddressCopied";
            public const string SeedPhraseCopied = "SeedPhraseCopied";

            public const string MnemonicTooltip = "MnemonicTooltip";
            public const string PassphraseTooltip = "PassphraseTooltip";
            public const string DerivationPathTooltip = "DerivationPathTooltip";

            public const string SettingsSubtitle = "SettingsSubtitle";
            public const string SecuritySubtitle = "SecuritySubtitle";
            public const string SectionComingSoon = "SectionComingSoon";
        }

        public static readonly Dictionary<string, string> DefaultValues = new()
        {
            { Keys.MnemonicDetails, "Seed Phrase Details" },
            { Keys.Overview, "Overview" },
            { Keys.Security, "Security" },
            { Keys.Accounts, "Accounts" },
            { Keys.Settings, "Settings" },

            { Keys.MnemonicOverview, "Seed Phrase Overview" },
            { Keys.MnemonicName, "Name" },
            { Keys.MnemonicType, "HD Wallet (BIP-44)" },
            { Keys.TypeLabel, "Type" },
            { Keys.SecurityLabel, "Security" },
            { Keys.CreatedDate, "Created" },
            { Keys.AccountCount, "Accounts" },
            { Keys.HasPassphrase, "Protected with passphrase" },
            { Keys.NoPassphrase, "No passphrase" },

            { Keys.AssociatedAccounts, "Associated Accounts" },
            { Keys.NoAccountsMessage, "No accounts have been created from this seed phrase yet." },
            { Keys.CreateFirstAccount, "Create First Account" },
            { Keys.AccountName, "Account Name" },
            { Keys.AccountIndex, "Account Index" },
            { Keys.DerivationPath, "Derivation Path" },
            { Keys.Address, "Address" },

            { Keys.RevealSeedPhrase, "Reveal Seed Phrase" },
            { Keys.SecurityWarning, "Security Warning" },
            { Keys.SecurityWarningMessage, "Your seed phrase gives full access to your wallet. Never share it with anyone and store it securely." },
            { Keys.SeedPhraseTitle, "Seed Phrase" },
            { Keys.KeepSecure, "Keep Secure" },
            { Keys.KeepSecureMessage, "Write down your seed phrase and store it in a secure location. Never share it with anyone." },
            { Keys.HideSeedPhrase, "Hide Seed Phrase" },

            { Keys.MnemonicLabel, "Seed Phrase Name" },
            { Keys.MnemonicLabelPlaceholder, "Enter a name for this seed phrase" },
            { Keys.MnemonicLabelHelperText, "Choose a memorable name to help you identify this seed phrase" },
            { Keys.SaveLabel, "Save Name" },
            { Keys.DeleteMnemonic, "Delete Seed Phrase" },
            { Keys.DeleteWarning, "Delete Seed Phrase" },
            { Keys.DeleteWarningMessage, "This will permanently delete the seed phrase and cannot be undone. Make sure you have it backed up." },
            { Keys.DeleteWarningWithAccountsMessage, "This will permanently delete this seed phrase and all {0} associated accounts. This action cannot be undone." },
            { Keys.CannotDeleteWithAccounts, "Cannot delete seed phrase that has associated accounts. Remove all accounts first." },

            { Keys.EditLabel, "Edit Name" },
            { Keys.ViewSeedPhrase, "View Seed Phrase" },
            { Keys.ManageAccounts, "Manage Accounts" },
            { Keys.AddAccount, "Add Account" },
            { Keys.ViewAccount, "View Account" },
            { Keys.Back, "Back" },
            { Keys.Continue, "Continue" },
            { Keys.Save, "Save" },
            { Keys.Cancel, "Cancel" },
            { Keys.Delete, "Delete" },
            { Keys.Confirm, "Confirm" },

            { Keys.Loading, "Loading..." },
            { Keys.LoadingMnemonic, "Loading seed phrase details..." },
            { Keys.Error, "Error" },
            { Keys.Success, "Success" },
            { Keys.MnemonicNotFound, "Seed phrase not found" },
            { Keys.MnemonicNotFoundMessage, "The requested seed phrase could not be found." },
            { Keys.NoVaultAvailable, "No wallet available" },
            { Keys.LabelUpdated, "Seed phrase name updated successfully" },
            { Keys.MnemonicDeleted, "Seed phrase deleted successfully" },
            { Keys.PasswordLabel, "Password" },
            { Keys.InvalidPassword, "Invalid password" },

            { Keys.CopyToClipboard, "Copy to clipboard" },
            { Keys.AddressCopied, "Address copied to clipboard" },
            { Keys.SeedPhraseCopied, "Seed phrase copied to clipboard" },

            { Keys.MnemonicTooltip, "A seed phrase is a series of words that can be used to recover your wallet" },
            { Keys.PassphraseTooltip, "An additional passphrase provides extra security for your seed phrase" },
            { Keys.DerivationPathTooltip, "The path used to generate this account from the seed phrase" },

            { Keys.SettingsSubtitle, "Manage seed phrase settings and deletion" },
            { Keys.SecuritySubtitle, "View and manage your seed phrase" },
            { Keys.SectionComingSoon, "Section coming soon..." }
        };

        public static readonly Dictionary<string, string> SpanishValues = new()
        {
            { Keys.MnemonicDetails, "Detalles de la Frase Semilla" },
            { Keys.Overview, "Resumen" },
            { Keys.Security, "Seguridad" },
            { Keys.Accounts, "Cuentas" },
            { Keys.Settings, "Configuración" },

            { Keys.MnemonicOverview, "Resumen de la Frase Semilla" },
            { Keys.MnemonicName, "Nombre" },
            { Keys.MnemonicType, "Cartera HD (BIP-44)" },
            { Keys.TypeLabel, "Tipo" },
            { Keys.SecurityLabel, "Seguridad" },
            { Keys.CreatedDate, "Creada" },
            { Keys.AccountCount, "Cuentas" },
            { Keys.HasPassphrase, "Protegida con contraseña" },
            { Keys.NoPassphrase, "Sin contraseña" },

            { Keys.AssociatedAccounts, "Cuentas Asociadas" },
            { Keys.NoAccountsMessage, "Aún no se han creado cuentas a partir de esta frase semilla." },
            { Keys.CreateFirstAccount, "Crear Primera Cuenta" },
            { Keys.AccountName, "Nombre de la Cuenta" },
            { Keys.AccountIndex, "Índice de la Cuenta" },
            { Keys.DerivationPath, "Ruta de Derivación" },
            { Keys.Address, "Dirección" },

            { Keys.RevealSeedPhrase, "Revelar Frase Semilla" },
            { Keys.SecurityWarning, "Advertencia de Seguridad" },
            { Keys.SecurityWarningMessage, "Tu frase semilla da acceso completo a tu cartera. Nunca la compartas con nadie y guárdala de forma segura." },
            { Keys.SeedPhraseTitle, "Frase Semilla" },
            { Keys.KeepSecure, "Mantener Segura" },
            { Keys.KeepSecureMessage, "Anota tu frase semilla y guárdala en un lugar seguro. Nunca la compartas con nadie." },
            { Keys.HideSeedPhrase, "Ocultar Frase Semilla" },

            { Keys.MnemonicLabel, "Nombre de la Frase Semilla" },
            { Keys.MnemonicLabelPlaceholder, "Ingresa un nombre para esta frase semilla" },
            { Keys.MnemonicLabelHelperText, "Elige un nombre memorable para ayudarte a identificar esta frase semilla" },
            { Keys.SaveLabel, "Guardar Nombre" },
            { Keys.DeleteMnemonic, "Eliminar Frase Semilla" },
            { Keys.DeleteWarning, "Eliminar Frase Semilla" },
            { Keys.DeleteWarningMessage, "Esto eliminará permanentemente la frase semilla y no se puede deshacer. Asegúrate de tener una copia de seguridad." },
            { Keys.DeleteWarningWithAccountsMessage, "Esto eliminará permanentemente esta frase semilla y todas las {0} cuentas asociadas. Esta acción no se puede deshacer." },
            { Keys.CannotDeleteWithAccounts, "No se puede eliminar una frase semilla que tiene cuentas asociadas. Elimina primero todas las cuentas." },

            { Keys.EditLabel, "Editar Nombre" },
            { Keys.ViewSeedPhrase, "Ver Frase Semilla" },
            { Keys.ManageAccounts, "Administrar Cuentas" },
            { Keys.AddAccount, "Añadir Cuenta" },
            { Keys.ViewAccount, "Ver Cuenta" },
            { Keys.Back, "Atrás" },
            { Keys.Continue, "Continuar" },
            { Keys.Save, "Guardar" },
            { Keys.Cancel, "Cancelar" },
            { Keys.Delete, "Eliminar" },
            { Keys.Confirm, "Confirmar" },

            { Keys.Loading, "Cargando..." },
            { Keys.LoadingMnemonic, "Cargando detalles de la frase semilla..." },
            { Keys.Error, "Error" },
            { Keys.Success, "Éxito" },
            { Keys.MnemonicNotFound, "Frase semilla no encontrada" },
            { Keys.MnemonicNotFoundMessage, "No se pudo encontrar la frase semilla solicitada." },
            { Keys.NoVaultAvailable, "No hay cartera disponible" },
            { Keys.LabelUpdated, "Nombre de la frase semilla actualizado exitosamente" },
            { Keys.MnemonicDeleted, "Frase semilla eliminada exitosamente" },
            { Keys.PasswordLabel, "Contraseña" },
            { Keys.InvalidPassword, "Contraseña inválida" },

            { Keys.CopyToClipboard, "Copiar al portapapeles" },
            { Keys.AddressCopied, "Dirección copiada al portapapeles" },
            { Keys.SeedPhraseCopied, "Frase semilla copiada al portapapeles" },

            { Keys.MnemonicTooltip, "Una frase semilla es una serie de palabras que se pueden usar para recuperar tu cartera" },
            { Keys.PassphraseTooltip, "Una contraseña adicional proporciona seguridad extra para tu frase semilla" },
            { Keys.DerivationPathTooltip, "La ruta utilizada para generar esta cuenta a partir de la frase semilla" },

            { Keys.SettingsSubtitle, "Administrar configuración y eliminación de la frase semilla" },
            { Keys.SecuritySubtitle, "Ver y administrar tu frase semilla" },
            { Keys.SectionComingSoon, "Sección próximamente..." }
        };
    }
}