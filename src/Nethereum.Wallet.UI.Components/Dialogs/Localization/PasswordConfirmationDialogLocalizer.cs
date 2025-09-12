using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Dialogs.Localization
{
    public class PasswordConfirmationDialogLocalizer : ComponentLocalizerBase<object>
    {
        public static class Keys
        {
            public const string WalletPasswordLabel = "WalletPasswordLabel";
            public const string PasswordRequiredError = "PasswordRequiredError";
            public const string PasswordHelperText = "PasswordHelperText";
            public const string ConfirmButtonText = "ConfirmButtonText";
            public const string CancelButtonText = "CancelButtonText";
            public const string TogglePasswordVisibilityLabel = "TogglePasswordVisibilityLabel";
            public const string DefaultTitle = "DefaultTitle";
            public const string DefaultMessage = "DefaultMessage";
            public const string InvalidPasswordError = "InvalidPasswordError";
            public const string ValidationError = "ValidationError";
        }
        
        public PasswordConfirmationDialogLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }
        
        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.WalletPasswordLabel] = "Wallet Password",
                [Keys.PasswordRequiredError] = "Password is required",
                [Keys.PasswordHelperText] = "Enter your wallet password to confirm this action",
                [Keys.ConfirmButtonText] = "Confirm",
                [Keys.CancelButtonText] = "Cancel",
                [Keys.TogglePasswordVisibilityLabel] = "Toggle password visibility",
                [Keys.DefaultTitle] = "Security Confirmation",
                [Keys.DefaultMessage] = "Please enter your wallet password to continue with this security-sensitive operation.",
                [Keys.InvalidPasswordError] = "Invalid password",
                [Keys.ValidationError] = "Validation error: {0}"
            });
            
            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.WalletPasswordLabel] = "Contraseña de Cartera",
                [Keys.PasswordRequiredError] = "La contraseña es requerida",
                [Keys.PasswordHelperText] = "Ingresa tu contraseña de cartera para confirmar esta acción",
                [Keys.ConfirmButtonText] = "Confirmar",
                [Keys.CancelButtonText] = "Cancelar",
                [Keys.TogglePasswordVisibilityLabel] = "Alternar visibilidad de contraseña",
                [Keys.DefaultTitle] = "Confirmación de Seguridad",
                [Keys.DefaultMessage] = "Por favor ingresa tu contraseña de cartera para continuar con esta operación sensible de seguridad.",
                [Keys.InvalidPasswordError] = "Contraseña inválida",
                [Keys.ValidationError] = "Error de validación: {0}"
            });
        }
    }
}