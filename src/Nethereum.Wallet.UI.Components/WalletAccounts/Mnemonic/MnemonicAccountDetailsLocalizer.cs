using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.Mnemonic
{
    public class MnemonicAccountDetailsLocalizer : ComponentLocalizerBase<MnemonicAccountDetailsViewModel>
    {

        public static class Keys
        {
            public const string DisplayName = "DisplayName";
            public const string Description = "Description";
            public const string GeneralTab = "GeneralTab";
            public const string SecurityTab = "SecurityTab";
            public const string Account = "Account";
            public const string AccountName = "AccountName";
            public const string AccountNamePlaceholder = "AccountNamePlaceholder";
            public const string AccountAddress = "AccountAddress";
            public const string DerivationPath = "DerivationPath";
            public const string AccountIndex = "AccountIndex";
            public const string EditAccountName = "EditAccountName";
            public const string SaveChanges = "SaveChanges";
            public const string Cancel = "Cancel";
            public const string SetAsActive = "SetAsActive";
            public const string CurrentlyActive = "CurrentlyActive";
            public const string RemoveAccount = "RemoveAccount";
            public const string CannotRemoveLastAccount = "CannotRemoveLastAccount";
            public const string SecurityWarning = "SecurityWarning";
            public const string SecurityWarningMessage = "SecurityWarningMessage";
            public const string RevealPrivateKey = "RevealPrivateKey";
            public const string EnterPasswordTitle = "EnterPasswordTitle";
            public const string EnterPasswordMessage = "EnterPasswordMessage";
            public const string RevealKeyButton = "RevealKeyButton";
            public const string PrivateKeyTitle = "PrivateKeyTitle";
            public const string KeepSecure = "KeepSecure";
            public const string KeepSecureMessage = "KeepSecureMessage";
            public const string CopyToClipboard = "CopyToClipboard";
            public const string CopiedToClipboard = "CopiedToClipboard";
            public const string FailedToCopy = "FailedToCopy";
            public const string InvalidPassword = "InvalidPassword";
            public const string PrivateKeyRetrievalError = "PrivateKeyRetrievalError";
            public const string AccountNameRequired = "AccountNameRequired";
            public const string AccountNameUpdated = "AccountNameUpdated";
            public const string AccountNameUpdateError = "AccountNameUpdateError";
            public const string ConfirmRemoval = "ConfirmRemoval";
            public const string ConfirmRemovalMessage = "ConfirmRemovalMessage";
            public const string ConfirmRemovalWarning = "ConfirmRemovalWarning";
            public const string AccountRemoved = "AccountRemoved";
            public const string RemovalError = "RemovalError";
            
            public const string AccountDetails = "AccountDetails";
            public const string MnemonicAccountType = "MnemonicAccountType";
            public const string AccountInfo = "AccountInfo";
            public const string ViewPrivateKey = "ViewPrivateKey";
            public const string ChangeAccountName = "ChangeAccountName";
            public const string BackToAccounts = "BackToAccounts";
            public const string Back = "Back";
            public const string Continue = "Continue";
            public const string SecuritySection = "SecuritySection";
            public const string ViewPrivateKeySubtitle = "ViewPrivateKeySubtitle";
            public const string HideKey = "HideKey";
            public const string LoadingAccount = "LoadingAccount";
            public const string Error = "Error";
            public const string Success = "Success";
            public const string NoAccountSelected = "NoAccountSelected";
            public const string SelectAccountMessage = "SelectAccountMessage";
            public const string PasswordLabel = "PasswordLabel";
            public const string AccountNameLabel = "AccountNameLabel";
            public const string AccountNameHelperText = "AccountNameHelperText";

            public const string AccountDerivedFromSeedPhrase = "AccountDerivedFromSeedPhrase";
            public const string AccountNotFoundInVault = "AccountNotFoundInVault";
        }

        public MnemonicAccountDetailsLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "HD Wallet Account",
                [Keys.Description] = "Account derived from seed phrase",
                [Keys.GeneralTab] = "General",
                [Keys.SecurityTab] = "Security",
                [Keys.Account] = "Account",
                [Keys.AccountName] = "Account Name",
                [Keys.AccountNamePlaceholder] = "Enter account name",
                [Keys.AccountAddress] = "Account Address",
                [Keys.DerivationPath] = "Derivation Path",
                [Keys.AccountIndex] = "Account Index",
                [Keys.EditAccountName] = "Edit Name",
                [Keys.SaveChanges] = "Save Changes",
                [Keys.Cancel] = "Cancel",
                [Keys.SetAsActive] = "Set as Active Account",
                [Keys.CurrentlyActive] = "Currently Active",
                [Keys.RemoveAccount] = "Remove Account",
                [Keys.CannotRemoveLastAccount] = "Cannot remove the last account",
                [Keys.SecurityWarning] = "Security Warning",
                [Keys.SecurityWarningMessage] = "Never share your private key with anyone. Only reveal it in a secure environment.",
                [Keys.RevealPrivateKey] = "Reveal Private Key",
                [Keys.EnterPasswordTitle] = "Enter Password",
                [Keys.EnterPasswordMessage] = "Enter your wallet password to reveal the private key for this account.",
                [Keys.RevealKeyButton] = "Reveal Key",
                [Keys.PrivateKeyTitle] = "Private Key",
                [Keys.KeepSecure] = "Keep this information secure!",
                [Keys.KeepSecureMessage] = "Never share it with anyone or store it in an insecure location.",
                [Keys.CopyToClipboard] = "Copy to Clipboard",
                [Keys.CopiedToClipboard] = "Copied to clipboard",
                [Keys.FailedToCopy] = "Failed to copy to clipboard",
                [Keys.InvalidPassword] = "Invalid password. Please try again.",
                [Keys.PrivateKeyRetrievalError] = "Unable to retrieve private key for this account.",
                [Keys.AccountNameRequired] = "Account name is required",
                [Keys.AccountNameUpdated] = "Account name updated successfully",
                [Keys.AccountNameUpdateError] = "Failed to update account name: {0}",
                [Keys.ConfirmRemoval] = "Confirm Account Removal",
                [Keys.ConfirmRemovalMessage] = "Are you sure you want to remove '{0}'?",
                [Keys.ConfirmRemovalWarning] = "This action cannot be undone. Make sure you have backed up your seed phrase.",
                [Keys.AccountRemoved] = "Account removed successfully",
                [Keys.RemovalError] = "Failed to remove account: {0}",
                
                [Keys.AccountDetails] = "Account Details",
                [Keys.MnemonicAccountType] = "HD Wallet Account",
                [Keys.AccountInfo] = "Account Information",
                [Keys.ViewPrivateKey] = "Private Key",
                [Keys.ChangeAccountName] = "Change the name of this account",
                [Keys.BackToAccounts] = "Back to Accounts",
                [Keys.Back] = "Back",
                [Keys.Continue] = "Continue",
                [Keys.SecuritySection] = "Security",
                [Keys.ViewPrivateKeySubtitle] = "Reveal your private key securely",
                [Keys.HideKey] = "Hide Key",
                [Keys.LoadingAccount] = "Loading account...",
                [Keys.Error] = "Error",
                [Keys.Success] = "Success",
                [Keys.NoAccountSelected] = "No Account Selected",
                [Keys.SelectAccountMessage] = "Please select an account to view its details",
                [Keys.PasswordLabel] = "Password",
                [Keys.AccountNameLabel] = "Account Name",
                [Keys.AccountNameHelperText] = "Give your account a memorable name",
                [Keys.AccountDerivedFromSeedPhrase] = "{0} is account index {1} derived from your {2} seed phrase.",
                [Keys.AccountNotFoundInVault] = "Account not found in vault."
            });

            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "Cuenta de Cartera HD",
                [Keys.Description] = "Cuenta derivada de frase semilla",
                [Keys.GeneralTab] = "General",
                [Keys.SecurityTab] = "Seguridad",
                [Keys.Account] = "Cuenta",
                [Keys.AccountName] = "Nombre de la Cuenta",
                [Keys.AccountNamePlaceholder] = "Ingresa el nombre de la cuenta",
                [Keys.AccountAddress] = "Dirección de la Cuenta",
                [Keys.DerivationPath] = "Ruta de Derivación",
                [Keys.AccountIndex] = "Índice de la Cuenta",
                [Keys.EditAccountName] = "Editar",
                [Keys.SaveChanges] = "Guardar Cambios",
                [Keys.Cancel] = "Cancelar",
                [Keys.SetAsActive] = "Establecer como Cuenta Activa",
                [Keys.CurrentlyActive] = "Actualmente Activa",
                [Keys.RemoveAccount] = "Eliminar Cuenta",
                [Keys.CannotRemoveLastAccount] = "No se puede eliminar la última cuenta",
                [Keys.SecurityWarning] = "Advertencia de Seguridad",
                [Keys.SecurityWarningMessage] = "Nunca compartas tu clave privada con nadie. Solo revélala en un entorno seguro.",
                [Keys.RevealPrivateKey] = "Revelar Clave Privada",
                [Keys.EnterPasswordTitle] = "Ingresa Contraseña",
                [Keys.EnterPasswordMessage] = "Ingresa la contraseña de tu cartera para revelar la clave privada de esta cuenta.",
                [Keys.RevealKeyButton] = "Revelar Clave",
                [Keys.PrivateKeyTitle] = "Clave Privada",
                [Keys.KeepSecure] = "¡Mantén esta información segura!",
                [Keys.KeepSecureMessage] = "Nunca la compartas con nadie ni la almacenes en un lugar inseguro.",
                [Keys.CopyToClipboard] = "Copiar al Portapapeles",
                [Keys.CopiedToClipboard] = "Copiado al portapapeles",
                [Keys.FailedToCopy] = "Error al copiar al portapapeles",
                [Keys.InvalidPassword] = "Contraseña inválida. Por favor intenta de nuevo.",
                [Keys.PrivateKeyRetrievalError] = "No se pudo obtener la clave privada para esta cuenta.",
                [Keys.AccountNameRequired] = "El nombre de la cuenta es requerido",
                [Keys.AccountNameUpdated] = "Nombre de la cuenta actualizado exitosamente",
                [Keys.AccountNameUpdateError] = "Error al actualizar el nombre de la cuenta: {0}",
                [Keys.ConfirmRemoval] = "Confirmar Eliminación de Cuenta",
                [Keys.ConfirmRemovalMessage] = "¿Estás seguro de que quieres eliminar '{0}'?",
                [Keys.ConfirmRemovalWarning] = "Esta acción no se puede deshacer. Asegúrate de haber respaldado tu frase semilla.",
                [Keys.AccountRemoved] = "Cuenta eliminada exitosamente",
                [Keys.RemovalError] = "Error al eliminar la cuenta: {0}",
                
                [Keys.AccountDetails] = "Detalles de Cuenta",
                [Keys.MnemonicAccountType] = "Cuenta de Cartera HD",
                [Keys.AccountInfo] = "Información de Cuenta",
                [Keys.ViewPrivateKey] = "Clave Privada",
                [Keys.ChangeAccountName] = "Cambiar el nombre de esta cuenta",
                [Keys.BackToAccounts] = "Volver a Cuentas",
                [Keys.Back] = "Atrás",
                [Keys.Continue] = "Continuar",
                [Keys.SecuritySection] = "Seguridad",
                [Keys.ViewPrivateKeySubtitle] = "Revelar tu clave privada de forma segura",
                [Keys.HideKey] = "Ocultar Clave",
                [Keys.LoadingAccount] = "Cargando cuenta...",
                [Keys.Error] = "Error",
                [Keys.Success] = "Éxito",
                [Keys.NoAccountSelected] = "Ninguna Cuenta Seleccionada",
                [Keys.SelectAccountMessage] = "Por favor selecciona una cuenta para ver sus detalles",
                [Keys.PasswordLabel] = "Contraseña",
                [Keys.AccountNameLabel] = "Nombre de Cuenta",
                [Keys.AccountNameHelperText] = "Dale un nombre memorable a tu cuenta",
                [Keys.AccountDerivedFromSeedPhrase] = "{0} es el índice de cuenta {1} derivado de tu frase semilla {2}.",
                [Keys.AccountNotFoundInVault] = "Cuenta no encontrada en la bóveda."
            });
        }
    }
}