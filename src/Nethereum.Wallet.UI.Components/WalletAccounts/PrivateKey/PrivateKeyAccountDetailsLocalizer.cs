using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.PrivateKey
{
    public class PrivateKeyAccountDetailsLocalizer : ComponentLocalizerBase<PrivateKeyAccountDetailsViewModel>
    {
        public static class Keys
        {
            public const string AccountDetails = "AccountDetails";
            public const string PrivateKeyAccountType = "PrivateKeyAccountType";
            public const string EditAccountName = "EditAccountName";
            public const string ChangeAccountName = "ChangeAccountName";
            public const string ViewPrivateKey = "ViewPrivateKey";
            public const string ViewPrivateKeySubtitle = "ViewPrivateKeySubtitle";
            public const string SecuritySection = "SecuritySection";
            public const string BackToAccounts = "BackToAccounts";
            public const string Back = "Back";
            public const string Continue = "Continue";
            public const string SaveChanges = "SaveChanges";
            public const string RevealKeyButton = "RevealKeyButton";
            public const string HideKey = "HideKey";
            public const string LoadingAccount = "LoadingAccount";
            public const string NoAccountSelected = "NoAccountSelected";
            public const string SelectAccountMessage = "SelectAccountMessage";
            public const string AccountNameLabel = "AccountNameLabel";
            public const string AccountNamePlaceholder = "AccountNamePlaceholder";
            public const string AccountNameHelperText = "AccountNameHelperText";
            public const string AccountNameRequired = "AccountNameRequired";
            public const string RevealPrivateKey = "RevealPrivateKey";
            public const string SecurityWarning = "SecurityWarning";
            public const string SecurityWarningMessage = "SecurityWarningMessage";
            public const string PasswordLabel = "PasswordLabel";
            public const string PrivateKeyTitle = "PrivateKeyTitle";
            public const string KeepSecure = "KeepSecure";
            public const string KeepSecureMessage = "KeepSecureMessage";
            public const string CopyToClipboard = "CopyToClipboard";
            public const string CopiedToClipboard = "CopiedToClipboard";
            public const string Error = "Error";
            public const string Success = "Success";
            public const string AccountImportedFrom = "AccountImportedFrom";
            
            public const string RemoveAccount = "RemoveAccount";
            public const string ConfirmRemoval = "ConfirmRemoval";
            public const string ConfirmRemovalMessage = "ConfirmRemovalMessage";
            public const string CannotRemoveLastAccount = "CannotRemoveLastAccount";
            public const string AccountRemoved = "AccountRemoved";
            public const string RemovalError = "RemovalError";
            public const string UnableToRetrievePrivateKey = "UnableToRetrievePrivateKey";
            public const string InvalidPassword = "InvalidPassword";
            public const string AccountNotFoundInVault = "AccountNotFoundInVault";
            public const string AccountNameUpdated = "AccountNameUpdated";
        }

        public PrivateKeyAccountDetailsLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }

        protected override void RegisterTranslations()
        {
            // English (US) translations
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.AccountDetails] = "Account Details",
                [Keys.PrivateKeyAccountType] = "Private Key Account",
                [Keys.EditAccountName] = "Edit Account Name",
                [Keys.ChangeAccountName] = "Change the name of this account",
                [Keys.ViewPrivateKey] = "View Private Key",
                [Keys.ViewPrivateKeySubtitle] = "Access your private key with password verification",
                [Keys.SecuritySection] = "Security",
                [Keys.BackToAccounts] = "Back to Accounts",
                [Keys.Back] = "Back",
                [Keys.Continue] = "Continue",
                [Keys.SaveChanges] = "Save Changes",
                [Keys.RevealKeyButton] = "Reveal Key",
                [Keys.HideKey] = "Hide Key",
                [Keys.LoadingAccount] = "Loading account details...",
                [Keys.NoAccountSelected] = "No Account Selected",
                [Keys.SelectAccountMessage] = "Please select an account to view its details.",
                [Keys.AccountNameLabel] = "Account Name",
                [Keys.AccountNamePlaceholder] = "Enter account name",
                [Keys.AccountNameHelperText] = "Give your account a memorable name",
                [Keys.AccountNameRequired] = "Account name is required",
                [Keys.RevealPrivateKey] = "Reveal Private Key",
                [Keys.SecurityWarning] = "Security Warning",
                [Keys.SecurityWarningMessage] = "Never share your private key with anyone. Anyone with access to your private key can control your account.",
                [Keys.PasswordLabel] = "Password",
                [Keys.PrivateKeyTitle] = "Private Key",
                [Keys.KeepSecure] = "Keep this information secure!",
                [Keys.KeepSecureMessage] = "Never share it with anyone or store it in an insecure location.",
                [Keys.CopyToClipboard] = "Copy to clipboard",
                [Keys.CopiedToClipboard] = "Copied to clipboard",
                [Keys.Error] = "Error",
                [Keys.Success] = "Success",
                [Keys.AccountImportedFrom] = "{0} was imported from a private key.",
                
                [Keys.RemoveAccount] = "Remove Account",
                [Keys.ConfirmRemoval] = "Confirm Account Removal",
                [Keys.ConfirmRemovalMessage] = "Are you sure you want to remove '{0}'? This action cannot be undone.",
                [Keys.CannotRemoveLastAccount] = "Cannot remove the last account in the vault.",
                [Keys.AccountRemoved] = "Account removed successfully",
                [Keys.RemovalError] = "Failed to remove account: {0}",
                [Keys.UnableToRetrievePrivateKey] = "Unable to retrieve private key for this account type.",
                [Keys.InvalidPassword] = "Invalid password.",
                [Keys.AccountNotFoundInVault] = "Account not found in vault.",
                [Keys.AccountNameUpdated] = "Account name updated successfully"
            });
            
            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.AccountDetails] = "Detalles de Cuenta",
                [Keys.PrivateKeyAccountType] = "Cuenta de Clave Privada",
                [Keys.EditAccountName] = "Editar Nombre de Cuenta",
                [Keys.ChangeAccountName] = "Cambiar el nombre de esta cuenta",
                [Keys.ViewPrivateKey] = "Ver Clave Privada",
                [Keys.ViewPrivateKeySubtitle] = "Accede a tu clave privada con verificación de contraseña",
                [Keys.SecuritySection] = "Seguridad",
                [Keys.BackToAccounts] = "Volver a Cuentas",
                [Keys.Back] = "Atrás",
                [Keys.Continue] = "Continuar",
                [Keys.SaveChanges] = "Guardar Cambios",
                [Keys.RevealKeyButton] = "Revelar Clave",
                [Keys.HideKey] = "Ocultar Clave",
                [Keys.LoadingAccount] = "Cargando detalles de la cuenta...",
                [Keys.NoAccountSelected] = "Ninguna Cuenta Seleccionada",
                [Keys.SelectAccountMessage] = "Por favor selecciona una cuenta para ver sus detalles.",
                [Keys.AccountNameLabel] = "Nombre de Cuenta",
                [Keys.AccountNamePlaceholder] = "Ingresa el nombre de la cuenta",
                [Keys.AccountNameHelperText] = "Dale un nombre memorable a tu cuenta",
                [Keys.AccountNameRequired] = "El nombre de cuenta es requerido",
                [Keys.RevealPrivateKey] = "Revelar Clave Privada",
                [Keys.SecurityWarning] = "Advertencia de Seguridad",
                [Keys.SecurityWarningMessage] = "Nunca compartas tu clave privada con nadie. Cualquiera con acceso a tu clave privada puede controlar tu cuenta.",
                [Keys.PasswordLabel] = "Contraseña",
                [Keys.PrivateKeyTitle] = "Clave Privada",
                [Keys.KeepSecure] = "¡Mantén esta información segura!",
                [Keys.KeepSecureMessage] = "Nunca la compartas con nadie o la almacenes en un lugar inseguro.",
                [Keys.CopyToClipboard] = "Copiar al portapapeles",
                [Keys.CopiedToClipboard] = "Copiado al portapapeles",
                [Keys.Error] = "Error",
                [Keys.Success] = "Éxito",
                [Keys.AccountImportedFrom] = "{0} fue importado desde una clave privada.",
                
                [Keys.RemoveAccount] = "Eliminar Cuenta",
                [Keys.ConfirmRemoval] = "Confirmar Eliminación de Cuenta",
                [Keys.ConfirmRemovalMessage] = "¿Estás seguro de que quieres eliminar '{0}'? Esta acción no se puede deshacer.",
                [Keys.CannotRemoveLastAccount] = "No se puede eliminar la última cuenta en la bóveda.",
                [Keys.AccountRemoved] = "Cuenta eliminada exitosamente",
                [Keys.RemovalError] = "Error al eliminar cuenta: {0}",
                [Keys.UnableToRetrievePrivateKey] = "No se puede obtener la clave privada para este tipo de cuenta.",
                [Keys.InvalidPassword] = "Contraseña inválida.",
                [Keys.AccountNotFoundInVault] = "Cuenta no encontrada en la bóveda.",
                [Keys.AccountNameUpdated] = "Nombre de cuenta actualizado exitosamente"
            });
        }
    }
}