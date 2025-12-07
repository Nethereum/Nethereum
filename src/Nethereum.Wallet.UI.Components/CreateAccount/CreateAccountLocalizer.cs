using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.CreateAccount
{
    public class CreateAccountLocalizer : ComponentLocalizerBase<CreateAccountViewModel>
    {
        public static class Keys
        {
            public const string PageTitle = "PageTitle";
            public const string PageDescription = "PageDescription";
            public const string SelectAccountType = "SelectAccountType";
            public const string CreateButton = "CreateButton";
            public const string CancelButton = "CancelButton";
            public const string CreatingAccount = "CreatingAccount";
            public const string AccountCreatedSuccessfully = "AccountCreatedSuccessfully";
            public const string FailedToCreateAccount = "FailedToCreateAccount";
            public const string VaultNotAvailable = "VaultNotAvailable";
            public const string NoAccountTypesAvailable = "NoAccountTypesAvailable";
            public const string AccountAddedSuccessfully = "AccountAddedSuccessfully";
            public const string AccountAddedDescription = "AccountAddedDescription";
            public const string AddAnotherAccount = "AddAnotherAccount";
        }
        
        public CreateAccountLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }
        
        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.PageTitle] = "Create New Account",
                [Keys.PageDescription] = "Choose the type of account you want to create",
                [Keys.SelectAccountType] = "Select Account Type",
                [Keys.CreateButton] = "Create Account",
                [Keys.CancelButton] = "Cancel",
                [Keys.CreatingAccount] = "Creating account...",
                [Keys.AccountCreatedSuccessfully] = "Account created successfully",
                [Keys.FailedToCreateAccount] = "Failed to create account: {0}",
                [Keys.VaultNotAvailable] = "Vault is not available",
                [Keys.NoAccountTypesAvailable] = "No account types are available",
                [Keys.AccountAddedSuccessfully] = "Account Added Successfully!",
                [Keys.AccountAddedDescription] = "Your new account has been added to your wallet",
                [Keys.AddAnotherAccount] = "Add Another Account"
            });
            
            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.PageTitle] = "Crear Nueva Cuenta",
                [Keys.PageDescription] = "Elige el tipo de cuenta que quieres crear",
                [Keys.SelectAccountType] = "Seleccionar Tipo de Cuenta",
                [Keys.CreateButton] = "Crear Cuenta",
                [Keys.CancelButton] = "Cancelar",
                [Keys.CreatingAccount] = "Creando cuenta...",
                [Keys.AccountCreatedSuccessfully] = "Cuenta creada exitosamente", 
                [Keys.FailedToCreateAccount] = "Error al crear la cuenta: {0}",
                [Keys.VaultNotAvailable] = "El almacén seguro no está disponible",
                [Keys.NoAccountTypesAvailable] = "No hay tipos de cuenta disponibles",
                [Keys.AccountAddedSuccessfully] = "¡Cuenta Agregada Exitosamente!",
                [Keys.AccountAddedDescription] = "Tu nueva cuenta ha sido agregada a tu cartera",
                [Keys.AddAnotherAccount] = "Agregar Otra Cuenta"
            });
        }
    }
}