using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.PrivateKey
{
    public class PrivateKeyAccountEditorLocalizer : ComponentLocalizerBase<PrivateKeyAccountCreationViewModel>
    {
        public static class Keys
        {
            public const string DisplayName = "DisplayName";
            public const string Description = "Description";
            public const string AccountNameLabel = "AccountNameLabel";
            public const string AccountNameHelperText = "AccountNameHelperText";
            public const string AccountNamePlaceholder = "AccountNamePlaceholder";
            public const string PrivateKeyLabel = "PrivateKeyLabel";
            public const string PrivateKeyHelperText = "PrivateKeyHelperText";
            public const string ToggleVisibilityLabel = "ToggleVisibilityLabel";
            public const string ValidFormatText = "ValidFormatText";
            public const string InvalidFormatText = "InvalidFormatText";
            public const string SecurityWarningTitle = "SecurityWarningTitle";
            public const string NeverShareAdvice = "NeverShareAdvice";
            public const string SecureEnvironmentAdvice = "SecureEnvironmentAdvice";
            public const string FullControlAdvice = "FullControlAdvice";
            public const string DerivedAddressLabel = "DerivedAddressLabel";
            public const string CopyAddressTitle = "CopyAddressTitle";
            public const string SupportedFormatsTitle = "SupportedFormatsTitle";
            public const string HexWithPrefixExample = "HexWithPrefixExample";
            public const string HexWithoutPrefixExample = "HexWithoutPrefixExample";
            public const string LengthRequirement = "LengthRequirement";
            public const string BackToLoginText = "BackToLoginText";
            public const string BackToAccountSelectionText = "BackToAccountSelectionText";
            public const string AddAccountText = "AddAccountText";
            public const string ValidPrivateKeyMessage = "ValidPrivateKeyMessage";
            public const string InvalidPrivateKeyMessage = "InvalidPrivateKeyMessage";
            
            public const string PrivateKeyRequiredError = "PrivateKeyRequiredError";
            public const string InvalidHexStringError = "InvalidHexStringError";
            public const string InvalidLengthError = "InvalidLengthError";
            public const string PrivateKeyCannotBeZeroError = "PrivateKeyCannotBeZeroError";
            public const string ValidPrivateKeySuccess = "ValidPrivateKeySuccess";
            public const string InvalidPrivateKeyError = "InvalidPrivateKeyError";
            public const string CreateAccountFailedError = "CreateAccountFailedError";
            
            public const string UnknownFormat = "UnknownFormat";
            public const string HexWithPrefixFormat = "HexWithPrefixFormat";
            public const string HexWithoutPrefixFormat = "HexWithoutPrefixFormat";
            public const string InvalidFormat = "InvalidFormat";
            
            public const string DefaultAccountName = "DefaultAccountName";
            
            public const string StepSetupLabel = "StepSetupLabel";
            public const string StepSetupDescription = "StepSetupDescription";
            public const string StepPrivateKeyLabel = "StepPrivateKeyLabel";
            public const string StepPrivateKeyDescription = "StepPrivateKeyDescription";
            public const string StepConfirmLabel = "StepConfirmLabel";
            public const string StepConfirmDescription = "StepConfirmDescription";
            
            public const string CreatePrivateKeyAccountTitle = "CreatePrivateKeyAccountTitle";
            public const string EnterPrivateKeyTitle = "EnterPrivateKeyTitle";
            public const string ConfirmAccountTitle = "ConfirmAccountTitle";
            public const string SetupAccountSubtitle = "SetupAccountSubtitle";
            public const string PrivateKeySubtitle = "PrivateKeySubtitle";
            public const string ConfirmSubtitle = "ConfirmSubtitle";
            
            public const string BackButtonText = "BackButtonText";
            public const string ContinueButtonText = "ContinueButtonText";
            public const string ExitButtonText = "ExitButtonText";
            
            public const string PrivateKeyImportTitle = "PrivateKeyImportTitle";
            public const string PrivateKeyImportDescription = "PrivateKeyImportDescription";
            
            public const string ReviewAccountTitle = "ReviewAccountTitle";
            public const string ImportantTitle = "ImportantTitle";
            public const string PrivateKeyBackupReminder = "PrivateKeyBackupReminder";
            public const string BackupConfirmationText = "BackupConfirmationText";
            
            public const string Error = "Error";
            public const string CopiedToClipboard = "CopiedToClipboard";
            
            public const string RemoveAccount = "RemoveAccount";
            public const string ConfirmRemoval = "ConfirmRemoval";
            public const string ConfirmRemovalMessage = "ConfirmRemovalMessage";
            public const string CannotRemoveLastAccount = "CannotRemoveLastAccount";
            public const string AccountRemoved = "AccountRemoved";
            public const string RemovalError = "RemovalError";
        }
        
        public PrivateKeyAccountEditorLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }
        
        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "Private Key Account",
                [Keys.Description] = "Create accounts by importing an existing private key",
                [Keys.AccountNameLabel] = "Account Name",
                [Keys.AccountNameHelperText] = "Give your account a memorable name (optional)",
                [Keys.AccountNamePlaceholder] = "My Account",
                [Keys.PrivateKeyLabel] = "Private Key",
                [Keys.PrivateKeyHelperText] = "Enter your private key (with or without 0x prefix)",
                [Keys.ToggleVisibilityLabel] = "Toggle private key visibility",
                [Keys.ValidFormatText] = "Valid Format",
                [Keys.InvalidFormatText] = "Invalid Format",
                [Keys.SecurityWarningTitle] = "Security Warning:",
                [Keys.NeverShareAdvice] = "• Never share your private key with anyone",
                [Keys.SecureEnvironmentAdvice] = "• Ensure you're in a secure environment",
                [Keys.FullControlAdvice] = "• Private keys provide full control over your account",
                [Keys.DerivedAddressLabel] = "Derived Address:",
                [Keys.CopyAddressTitle] = "Copy Address",
                [Keys.SupportedFormatsTitle] = "Supported Formats:",
                [Keys.HexWithPrefixExample] = "• Hex with 0x prefix: 0x1234abcd...",
                [Keys.HexWithoutPrefixExample] = "• Hex without prefix: 1234abcd...",
                [Keys.LengthRequirement] = "• Must be exactly 64 characters (32 bytes)",
                [Keys.BackToLoginText] = "Back to Login",
                [Keys.BackToAccountSelectionText] = "Back to Account Selection",
                [Keys.AddAccountText] = "Add Account",
                [Keys.ValidPrivateKeyMessage] = "Valid private key format",
                [Keys.InvalidPrivateKeyMessage] = "Invalid private key format",
                
                [Keys.PrivateKeyRequiredError] = "Private key is required",
                [Keys.InvalidHexStringError] = "Private key must be a valid hexadecimal string",
                [Keys.InvalidLengthError] = "Private key must be 64 characters long (32 bytes), got {0}",
                [Keys.PrivateKeyCannotBeZeroError] = "Private key cannot be zero",
                [Keys.ValidPrivateKeySuccess] = "Valid private key → {0}",
                [Keys.InvalidPrivateKeyError] = "Invalid private key: {0}",
                [Keys.CreateAccountFailedError] = "Failed to create account: {0}",
                
                [Keys.UnknownFormat] = "Unknown",
                [Keys.HexWithPrefixFormat] = "Hex with 0x prefix",
                [Keys.HexWithoutPrefixFormat] = "Hex without prefix",
                [Keys.InvalidFormat] = "Invalid format",
                
                [Keys.DefaultAccountName] = "Private Key Account",
                
                [Keys.StepSetupLabel] = "Setup",
                [Keys.StepSetupDescription] = "Configure account name",
                [Keys.StepPrivateKeyLabel] = "Private Key",
                [Keys.StepPrivateKeyDescription] = "Enter your private key",
                [Keys.StepConfirmLabel] = "Confirm",
                [Keys.StepConfirmDescription] = "Review and create",
                
                [Keys.CreatePrivateKeyAccountTitle] = "Create Private Key Account",
                [Keys.EnterPrivateKeyTitle] = "Enter Private Key",
                [Keys.ConfirmAccountTitle] = "Confirm Account Creation",
                [Keys.SetupAccountSubtitle] = "Configure your new private key account",
                [Keys.PrivateKeySubtitle] = "Import an existing private key",
                [Keys.ConfirmSubtitle] = "Review your account details before creation",
                
                [Keys.BackButtonText] = "Back",
                [Keys.ContinueButtonText] = "Continue",
                [Keys.ExitButtonText] = "Exit",
                
                [Keys.PrivateKeyImportTitle] = "Import Existing Private Key",
                [Keys.PrivateKeyImportDescription] = "Import an account by providing your existing private key. Make sure you're in a secure environment.",
                
                [Keys.ReviewAccountTitle] = "Review Account",
                [Keys.ImportantTitle] = "Important!",
                [Keys.PrivateKeyBackupReminder] = "Make sure you have securely backed up your private key. This is the only way to recover your account.",
                [Keys.BackupConfirmationText] = "I confirm that I have securely backed up my private key",
                
                [Keys.Error] = "Error",
                [Keys.CopiedToClipboard] = "Copied to clipboard",
                
                [Keys.RemoveAccount] = "Remove Account",
                [Keys.ConfirmRemoval] = "Confirm Account Removal",
                [Keys.ConfirmRemovalMessage] = "Are you sure you want to remove '{0}'? This action cannot be undone.",
                [Keys.CannotRemoveLastAccount] = "Cannot remove the last account in the vault.",
                [Keys.AccountRemoved] = "Account removed successfully",
                [Keys.RemovalError] = "Failed to remove account: {0}"
            });
            
            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "Cuenta de Clave Privada",
                [Keys.Description] = "Crear cuentas importando una clave privada existente",
                [Keys.AccountNameLabel] = "Nombre de Cuenta",
                [Keys.AccountNameHelperText] = "Dale un nombre memorable a tu cuenta (opcional)",
                [Keys.AccountNamePlaceholder] = "Mi Cuenta",
                [Keys.PrivateKeyLabel] = "Clave Privada",
                [Keys.PrivateKeyHelperText] = "Ingresa tu clave privada (con o sin prefijo 0x)",
                [Keys.ToggleVisibilityLabel] = "Alternar visibilidad de clave privada",
                [Keys.ValidFormatText] = "Formato Válido",
                [Keys.InvalidFormatText] = "Formato Inválido",
                [Keys.SecurityWarningTitle] = "Advertencia de Seguridad:",
                [Keys.NeverShareAdvice] = "• Nunca compartas tu clave privada con nadie",
                [Keys.SecureEnvironmentAdvice] = "• Asegúrate de estar en un entorno seguro",
                [Keys.FullControlAdvice] = "• Las claves privadas proporcionan control total sobre tu cuenta",
                [Keys.DerivedAddressLabel] = "Dirección Derivada:",
                [Keys.CopyAddressTitle] = "Copiar Dirección",
                [Keys.SupportedFormatsTitle] = "Formatos Soportados:",
                [Keys.HexWithPrefixExample] = "• Hex con prefijo 0x: 0x1234abcd...",
                [Keys.HexWithoutPrefixExample] = "• Hex sin prefijo: 1234abcd...",
                [Keys.LengthRequirement] = "• Debe tener exactamente 64 caracteres (32 bytes)",
                [Keys.BackToLoginText] = "Volver al Inicio de Sesión",
                [Keys.BackToAccountSelectionText] = "Volver a Selección de Cuenta",
                [Keys.AddAccountText] = "Añadir Cuenta",
                [Keys.ValidPrivateKeyMessage] = "Formato de clave privada válido",
                [Keys.InvalidPrivateKeyMessage] = "Formato de clave privada inválido",
                
                [Keys.PrivateKeyRequiredError] = "La clave privada es requerida",
                [Keys.InvalidHexStringError] = "La clave privada debe ser una cadena hexadecimal válida",
                [Keys.InvalidLengthError] = "La clave privada debe tener 64 caracteres (32 bytes), recibió {0}",
                [Keys.PrivateKeyCannotBeZeroError] = "La clave privada no puede ser cero",
                [Keys.ValidPrivateKeySuccess] = "Clave privada válida → {0}",
                [Keys.InvalidPrivateKeyError] = "Clave privada inválida: {0}",
                [Keys.CreateAccountFailedError] = "Error al crear cuenta: {0}",
                
                [Keys.UnknownFormat] = "Desconocido",
                [Keys.HexWithPrefixFormat] = "Hex con prefijo 0x",
                [Keys.HexWithoutPrefixFormat] = "Hex sin prefijo",
                [Keys.InvalidFormat] = "Formato inválido",
                
                [Keys.DefaultAccountName] = "Cuenta de Clave Privada",
                
                [Keys.StepSetupLabel] = "Configuración",
                [Keys.StepSetupDescription] = "Configurar nombre de cuenta",
                [Keys.StepPrivateKeyLabel] = "Clave Privada",
                [Keys.StepPrivateKeyDescription] = "Ingresa tu clave privada",
                [Keys.StepConfirmLabel] = "Confirmar",
                [Keys.StepConfirmDescription] = "Revisar y crear",
                
                [Keys.CreatePrivateKeyAccountTitle] = "Crear Cuenta de Clave Privada",
                [Keys.EnterPrivateKeyTitle] = "Ingresar Clave Privada",
                [Keys.ConfirmAccountTitle] = "Confirmar Creación de Cuenta",
                [Keys.SetupAccountSubtitle] = "Configura tu nueva cuenta de clave privada",
                [Keys.PrivateKeySubtitle] = "Importar una clave privada existente",
                [Keys.ConfirmSubtitle] = "Revisa los detalles de tu cuenta antes de crearla",
                
                [Keys.BackButtonText] = "Atrás",
                [Keys.ContinueButtonText] = "Continuar",
                [Keys.ExitButtonText] = "Salir",
                
                [Keys.PrivateKeyImportTitle] = "Importar Clave Privada Existente",
                [Keys.PrivateKeyImportDescription] = "Importa una cuenta proporcionando tu clave privada existente. Asegúrate de estar en un entorno seguro.",
                
                [Keys.ReviewAccountTitle] = "Revisar Cuenta",
                [Keys.ImportantTitle] = "¡Importante!",
                [Keys.PrivateKeyBackupReminder] = "Asegúrate de haber respaldado de forma segura tu clave privada. Esta es la única forma de recuperar tu cuenta.",
                [Keys.BackupConfirmationText] = "Confirmo que he respaldado de forma segura mi clave privada",
                
                [Keys.Error] = "Error",
                [Keys.CopiedToClipboard] = "Copiado al portapapeles",
                
                [Keys.RemoveAccount] = "Eliminar Cuenta",
                [Keys.ConfirmRemoval] = "Confirmar Eliminación de Cuenta",
                [Keys.ConfirmRemovalMessage] = "¿Estás seguro de que quieres eliminar '{0}'? Esta acción no se puede deshacer.",
                [Keys.CannotRemoveLastAccount] = "No se puede eliminar la última cuenta en la bóveda.",
                [Keys.AccountRemoved] = "Cuenta eliminada exitosamente",
                [Keys.RemovalError] = "Error al eliminar cuenta: {0}"
            });
        }
    }
}