using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.AccountList
{
    public class AccountManagementLocalizer : ComponentLocalizerBase<object>
    {
        public static class Keys
        {
            public const string MyAccounts = "MyAccounts";
            public const string ManageAccountsDescription = "ManageAccountsDescription";
            public const string LoadingAccounts = "LoadingAccounts";
            public const string ActiveAccount = "ActiveAccount";
            public const string Address = "Address";
            public const string CopyAddress = "CopyAddress";
            public const string EditAccount = "EditAccount";
            public const string NoAccountsFound = "NoAccountsFound";
            public const string NoAccountsDescription = "NoAccountsDescription";
            public const string AddFirstAccount = "AddFirstAccount";
            public const string AccountFallbackName = "AccountFallbackName";
            
            public const string MnemonicAccount = "MnemonicAccount";
            public const string PrivateKeyAccount = "PrivateKeyAccount";
            public const string ViewOnlyAccount = "ViewOnlyAccount";
            public const string UnknownAccount = "UnknownAccount";
            public const string SmartContractWallet = "SmartContractWallet";
            public const string HDWalletAccount = "HDWalletAccount";
            
            public const string AccountManagement = "AccountManagement";
            public const string AccountManagementDescription = "AccountManagementDescription";
            public const string AddAccount = "AddAccount";
            public const string SelectAccount = "SelectAccount";
            public const string RemoveAccount = "RemoveAccount";
            public const string CreateFirstAccount = "CreateFirstAccount";
            public const string RemoveAccountConfirmTitle = "RemoveAccountConfirmTitle";
            public const string RemoveAccountConfirmMessage = "RemoveAccountConfirmMessage";
            public const string RemoveAccountWarning = "RemoveAccountWarning";
            public const string AccountRemovedSuccess = "AccountRemovedSuccess";
            public const string RemoveAccountError = "RemoveAccountError";
            
            public const string AddNewAccount = "AddNewAccount";
            public const string ChooseAccountTypeDescription = "ChooseAccountTypeDescription";
            public const string BackButton = "BackButton";
            public const string CreateAccountType = "CreateAccountType";
            public const string AccountAddedSuccess = "AccountAddedSuccess";
            public const string AccountAddedDescription = "AccountAddedDescription";
            public const string AddAnotherAccount = "AddAnotherAccount";
            public const string AccountPlaceholderName = "AccountPlaceholderName";
            public const string AccountNameHelperText = "AccountNameHelperText";
            public const string CreateAccountError = "CreateAccountError";
            public const string VaultNotAvailable = "VaultNotAvailable";
            
            public const string EditAccountName = "EditAccountName";
            public const string EditAccountDescription = "EditAccountDescription";
            public const string AccountAddress = "AccountAddress";
            public const string AccountName = "AccountName";
            public const string AccountNamePlaceholder = "AccountNamePlaceholder";
            public const string CancelButton = "CancelButton";
            public const string SaveChanges = "SaveChanges";
            public const string EnterDifferentName = "EnterDifferentName";
            public const string AccountNameRequired = "AccountNameRequired";
            public const string AccountNameMinLength = "AccountNameMinLength";
            
            public const string Error = "Error";
            public const string SelectAccountError = "SelectAccountError";
            public const string AddressCopiedSuccess = "AddressCopiedSuccess";
            public const string CopyAddressFailed = "CopyAddressFailed";
        }
        
        public AccountManagementLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }
        
        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.MyAccounts] = "My Accounts",
                [Keys.ManageAccountsDescription] = "Manage and switch between your wallet accounts",
                [Keys.LoadingAccounts] = "Loading accounts...",
                [Keys.ActiveAccount] = "Active",
                [Keys.Address] = "Address:",
                [Keys.CopyAddress] = "Copy Address",
                [Keys.EditAccount] = "Edit",
                [Keys.NoAccountsFound] = "No Accounts Found",
                [Keys.NoAccountsDescription] = "Get started by adding your first account",
                [Keys.AddFirstAccount] = "Add Your First Account",
                [Keys.AccountFallbackName] = "Account {0}",
                
                [Keys.MnemonicAccount] = "Mnemonic Account",
                [Keys.PrivateKeyAccount] = "Private Key Account",
                [Keys.ViewOnlyAccount] = "View Only Account",
                [Keys.UnknownAccount] = "Unknown Account",
                [Keys.SmartContractWallet] = "Smart Contract Wallet",
                [Keys.HDWalletAccount] = "HD Wallet Account (Index: {0})",
                
                [Keys.AccountManagement] = "Account Management",
                [Keys.AccountManagementDescription] = "Manage your wallet accounts - edit names, remove accounts, or set the active account",
                [Keys.AddAccount] = "Add Account",
                [Keys.SelectAccount] = "Select",
                [Keys.RemoveAccount] = "Remove",
                [Keys.CreateFirstAccount] = "Create First Account",
                [Keys.RemoveAccountConfirmTitle] = "Remove Account",
                [Keys.RemoveAccountConfirmMessage] = "Are you sure you want to remove the account '{0}'?",
                [Keys.RemoveAccountWarning] = "This action cannot be undone. Make sure you have backed up your private keys or seed phrase.",
                [Keys.AccountRemovedSuccess] = "Account removed successfully",
                [Keys.RemoveAccountError] = "Error removing account: {0}",
                
                [Keys.AddNewAccount] = "Add New Account",
                [Keys.ChooseAccountTypeDescription] = "Choose the type of account you want to add to your wallet",
                [Keys.BackButton] = "Back",
                [Keys.CreateAccountType] = "Create {0}",
                [Keys.AccountAddedSuccess] = "Account Added Successfully!",
                [Keys.AccountAddedDescription] = "Your new account has been added to your wallet",
                [Keys.AddAnotherAccount] = "Add Another Account",
                [Keys.AccountPlaceholderName] = "Account {0}",
                [Keys.AccountNameHelperText] = "Give your account a memorable name (optional)",
                [Keys.CreateAccountError] = "Failed to create account: {0}",
                [Keys.VaultNotAvailable] = "Vault is not available",
                
                [Keys.EditAccountName] = "Edit Account Name",
                [Keys.EditAccountDescription] = "Update the display name for this account",
                [Keys.AccountAddress] = "Account Address",
                [Keys.AccountName] = "Account Name",
                [Keys.AccountNamePlaceholder] = "Enter a memorable name for this account",
                [Keys.CancelButton] = "Cancel",
                [Keys.SaveChanges] = "Save Changes",
                [Keys.EnterDifferentName] = "Please enter a different name",
                [Keys.AccountNameRequired] = "Account name cannot be empty",
                [Keys.AccountNameMinLength] = "Account name must be at least 1 character",
                
                [Keys.Error] = "Error",
                [Keys.SelectAccountError] = "Failed to select account: {0}",
                [Keys.AddressCopiedSuccess] = "Address copied to clipboard",
                [Keys.CopyAddressFailed] = "Failed to copy address"
            });
            
            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.MyAccounts] = "Mis Cuentas",
                [Keys.ManageAccountsDescription] = "Gestiona y cambia entre tus cuentas de cartera",
                [Keys.LoadingAccounts] = "Cargando cuentas...",
                [Keys.ActiveAccount] = "Activa",
                [Keys.Address] = "Dirección:",
                [Keys.CopyAddress] = "Copiar Dirección",
                [Keys.EditAccount] = "Editar",
                [Keys.NoAccountsFound] = "No Se Encontraron Cuentas",
                [Keys.NoAccountsDescription] = "Comienza añadiendo tu primera cuenta",
                [Keys.AddFirstAccount] = "Añade Tu Primera Cuenta",
                [Keys.AccountFallbackName] = "Cuenta {0}",
                
                [Keys.MnemonicAccount] = "Cuenta Mnemónica",
                [Keys.PrivateKeyAccount] = "Cuenta de Clave Privada",
                [Keys.ViewOnlyAccount] = "Cuenta de Solo Lectura",
                [Keys.UnknownAccount] = "Cuenta Desconocida",
                [Keys.SmartContractWallet] = "Cartera de Contrato Inteligente",
                [Keys.HDWalletAccount] = "Cuenta HD (Índice: {0})",
                
                [Keys.AccountManagement] = "Gestión de Cuentas",
                [Keys.AccountManagementDescription] = "Gestiona tus cuentas de cartera - edita nombres, elimina cuentas, o establece la cuenta activa",
                [Keys.AddAccount] = "Añadir Cuenta",
                [Keys.SelectAccount] = "Seleccionar",
                [Keys.RemoveAccount] = "Eliminar",
                [Keys.CreateFirstAccount] = "Crear Primera Cuenta",
                [Keys.RemoveAccountConfirmTitle] = "Eliminar Cuenta",
                [Keys.RemoveAccountConfirmMessage] = "¿Estás seguro de que quieres eliminar la cuenta '{0}'?",
                [Keys.RemoveAccountWarning] = "Esta acción no se puede deshacer. Asegúrate de haber respaldado tus claves privadas o frase semilla.",
                [Keys.AccountRemovedSuccess] = "Cuenta eliminada exitosamente",
                [Keys.RemoveAccountError] = "Error eliminando cuenta: {0}",
                
                [Keys.AddNewAccount] = "Añadir Nueva Cuenta",
                [Keys.ChooseAccountTypeDescription] = "Elige el tipo de cuenta que quieres añadir a tu cartera",
                [Keys.BackButton] = "Atrás",
                [Keys.CreateAccountType] = "Crear {0}",
                [Keys.AccountAddedSuccess] = "¡Cuenta Añadida Exitosamente!",
                [Keys.AccountAddedDescription] = "Tu nueva cuenta ha sido añadida a tu cartera",
                [Keys.AddAnotherAccount] = "Añadir Otra Cuenta",
                [Keys.AccountPlaceholderName] = "Cuenta {0}",
                [Keys.AccountNameHelperText] = "Dale un nombre memorable a tu cuenta (opcional)",
                [Keys.CreateAccountError] = "Error al crear cuenta: {0}",
                [Keys.VaultNotAvailable] = "La bóveda no está disponible",
                
                [Keys.EditAccountName] = "Editar Nombre de Cuenta",
                [Keys.EditAccountDescription] = "Actualiza el nombre de visualización para esta cuenta",
                [Keys.AccountAddress] = "Dirección de Cuenta",
                [Keys.AccountName] = "Nombre de Cuenta",
                [Keys.AccountNamePlaceholder] = "Ingresa un nombre memorable para esta cuenta",
                [Keys.CancelButton] = "Cancelar",
                [Keys.SaveChanges] = "Guardar Cambios",
                [Keys.EnterDifferentName] = "Por favor ingresa un nombre diferente",
                [Keys.AccountNameRequired] = "El nombre de cuenta no puede estar vacío",
                [Keys.AccountNameMinLength] = "El nombre de cuenta debe tener al menos 1 carácter",
                
                [Keys.Error] = "Error",
                [Keys.SelectAccountError] = "Error al seleccionar cuenta: {0}",
                [Keys.AddressCopiedSuccess] = "Dirección copiada al portapapeles",
                [Keys.CopyAddressFailed] = "Error al copiar dirección"
            });
        }
    }
}