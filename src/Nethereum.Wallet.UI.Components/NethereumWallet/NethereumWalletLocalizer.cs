using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.NethereumWallet
{
    public class NethereumWalletLocalizer : ComponentLocalizerBase<NethereumWalletViewModel>
    {
        public static class Keys
        {
            public const string LoadingText = "LoadingText";
            public const string LoginTitle = "LoginTitle";
            public const string LoginSubtitle = "LoginSubtitle";
            public const string PasswordLabel = "PasswordLabel";
            public const string PasswordPlaceholder = "PasswordPlaceholder";
            public const string LoginButton = "LoginButton";
            public const string CreateTitle = "CreateTitle";
            public const string CreateSubtitle = "CreateSubtitle";
            public const string CreatePasswordLabel = "CreatePasswordLabel";
            public const string ConfirmPasswordLabel = "ConfirmPasswordLabel";
            public const string PasswordHelperText = "PasswordHelperText";
            public const string CreateButtonText = "CreateButtonText";
            public const string CreateCancelButtonText = "CreateCancelButtonText";
            public const string CreateNewWalletLinkText = "CreateNewWalletLinkText";
            public const string ResetWallet = "ResetWallet";
            public const string ResetConfirmTitle = "ResetConfirmTitle";
            public const string ResetConfirmMessage = "ResetConfirmMessage";
            public const string ResetConfirmButton = "ResetConfirmButton";
            public const string ResetCancelButton = "ResetCancelButton";
            public const string PasswordRequired = "PasswordRequired";
            public const string PasswordMismatch = "PasswordMismatch";
            public const string PasswordTooShort = "PasswordTooShort";
            public const string IncorrectPassword = "IncorrectPassword";
            public const string LoginError = "LoginError";
            public const string VaultUnlockedSuccessfully = "VaultUnlockedSuccessfully";
            public const string WalletCreatedSuccessfully = "WalletCreatedSuccessfully";
            public const string WalletCreationFailed = "WalletCreationFailed";
        }
        
        public NethereumWalletLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }
        
        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.LoadingText] = "Setting up wallet...",
                [Keys.LoginTitle] = "Welcome Back",
                [Keys.LoginSubtitle] = "Enter your password to unlock your wallet",
                [Keys.PasswordLabel] = "Password",
                [Keys.PasswordPlaceholder] = "Enter your password",
                [Keys.LoginButton] = "Unlock Wallet",
                [Keys.CreateTitle] = "Create New Wallet",
                [Keys.CreateSubtitle] = "Set up a new wallet vault to securely store your accounts",
                [Keys.CreatePasswordLabel] = "Create Password",
                [Keys.ConfirmPasswordLabel] = "Confirm Password",
                [Keys.PasswordHelperText] = "Choose a strong password to protect your wallet",
                [Keys.CreateButtonText] = "Create Wallet",
                [Keys.CreateCancelButtonText] = "Cancel",
                [Keys.CreateNewWalletLinkText] = "Create New Wallet",
                [Keys.ResetWallet] = "Reset Wallet",
                [Keys.ResetConfirmTitle] = "Reset Wallet",
                [Keys.ResetConfirmMessage] = "Are you sure you want to reset your wallet? This will permanently delete all accounts and data. This action cannot be undone.",
                [Keys.ResetConfirmButton] = "Reset Wallet",
                [Keys.ResetCancelButton] = "Cancel",
                [Keys.PasswordRequired] = "Password is required",
                [Keys.PasswordMismatch] = "Passwords do not match",
                [Keys.PasswordTooShort] = "Password must be at least {0} characters long",
                [Keys.IncorrectPassword] = "Incorrect password",
                [Keys.LoginError] = "Login failed: {0}",
                [Keys.VaultUnlockedSuccessfully] = "Vault unlocked successfully",
                [Keys.WalletCreatedSuccessfully] = "Wallet created successfully",
                [Keys.WalletCreationFailed] = "Failed to create wallet: {0}"
            });
            
            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.LoadingText] = "Configurando cartera...",
                [Keys.LoginTitle] = "Bienvenido de vuelta",
                [Keys.LoginSubtitle] = "Introduce tu contraseña para desbloquear tu cartera",
                [Keys.PasswordLabel] = "Contraseña",
                [Keys.PasswordPlaceholder] = "Introduce tu contraseña",
                [Keys.LoginButton] = "Desbloquear Cartera",
                [Keys.CreateTitle] = "Crear Nueva Cartera",
                [Keys.CreateSubtitle] = "Configura una nueva caja fuerte para almacenar tus cuentas de forma segura",
                [Keys.CreatePasswordLabel] = "Crear Contraseña",
                [Keys.ConfirmPasswordLabel] = "Confirmar Contraseña",
                [Keys.PasswordHelperText] = "Elige una contraseña segura para proteger tu cartera",
                [Keys.CreateButtonText] = "Crear Cartera",
                [Keys.CreateCancelButtonText] = "Cancelar",
                [Keys.CreateNewWalletLinkText] = "Crear Nueva Cartera",
                [Keys.ResetWallet] = "Reiniciar Cartera",
                [Keys.ResetConfirmTitle] = "Reiniciar Cartera",
                [Keys.ResetConfirmMessage] = "¿Estás seguro de que quieres reiniciar tu cartera? Esto eliminará permanentemente todas las cuentas y datos. Esta acción no se puede deshacer.",
                [Keys.ResetConfirmButton] = "Reiniciar Cartera",
                [Keys.ResetCancelButton] = "Cancelar",
                [Keys.PasswordRequired] = "La contraseña es obligatoria",
                [Keys.PasswordMismatch] = "Las contraseñas no coinciden",
                [Keys.PasswordTooShort] = "La contraseña debe tener al menos {0} caracteres",
                [Keys.IncorrectPassword] = "Contraseña incorrecta",
                [Keys.LoginError] = "Error al iniciar sesión: {0}",
                [Keys.VaultUnlockedSuccessfully] = "Caja fuerte desbloqueada correctamente",
                [Keys.WalletCreatedSuccessfully] = "Cartera creada correctamente",
                [Keys.WalletCreationFailed] = "Error al crear la cartera: {0}"
            });
        }
    }
}
