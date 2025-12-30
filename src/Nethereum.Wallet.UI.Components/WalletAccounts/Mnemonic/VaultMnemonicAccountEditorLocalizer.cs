using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.Mnemonic
{
    public class VaultMnemonicAccountEditorLocalizer : ComponentLocalizerBase<VaultMnemonicAccountViewModel>
    {
        public static class Keys
        {
            public const string DisplayName = "DisplayName";
            public const string Description = "Description";
            public const string PageTitle = "PageTitle";
            public const string PageDescription = "PageDescription";
            public const string SelectMnemonicLabel = "SelectMnemonicLabel";
            public const string AccountIndexLabel = "AccountIndexLabel";
            public const string AccountIndexHelperText = "AccountIndexHelperText";
            public const string AccountNameLabel = "AccountNameLabel";
            public const string AccountNameHelperText = "AccountNameHelperText";
            public const string AccountNamePlaceholder = "AccountNamePlaceholder";
            public const string AddressPreviewTitle = "AddressPreviewTitle";
            public const string CopyAddressTitle = "CopyAddressTitle";
            public const string DerivationPathText = "DerivationPathText";
            public const string BackToLoginText = "BackToLoginText";
            public const string BackToAccountSelectionText = "BackToAccountSelectionText";
            public const string CreateAccountText = "CreateAccountText";
            public const string NoMnemonicsTitle = "NoMnemonicsTitle";
            public const string NoMnemonicsDescription = "NoMnemonicsDescription";
            public const string ErrorLoadingMnemonics = "ErrorLoadingMnemonics";
            public const string ErrorDerivingAddress = "ErrorDerivingAddress";
            
            public const string SelectMnemonicTitle = "SelectMnemonicTitle";
            public const string SelectMnemonicSubtitle = "SelectMnemonicSubtitle";
            public const string ConfigureAccountTitle = "ConfigureAccountTitle";
            public const string ConfigureAccountSubtitle = "ConfigureAccountSubtitle";
            public const string ConfirmDetailsTitle = "ConfirmDetailsTitle";
            public const string ConfirmDetailsSubtitle = "ConfirmDetailsSubtitle";
            public const string AddAccount = "AddAccount";
            public const string MnemonicSelection = "MnemonicSelection";
            public const string AccountConfiguration = "AccountConfiguration";
            public const string AccountConfirmation = "AccountConfirmation";
            public const string Exit = "Exit";
            public const string Back = "Back";
            public const string Continue = "Continue";
            public const string MnemonicRequired = "MnemonicRequired";
            public const string AccountIndexRequired = "AccountIndexRequired";
            public const string DerivedAccountPreview = "DerivedAccountPreview";
            public const string AccountType = "AccountType";
            public const string MnemonicDerivedAccount = "MnemonicDerivedAccount";
            public const string UnnamedAccount = "UnnamedAccount";
            public const string SecurityInfo = "SecurityInfo";
            public const string MnemonicSecurityNotice = "MnemonicSecurityNotice";
            public const string Error = "Error";
            
            public const string StepSelectMnemonicLabel = "StepSelectMnemonicLabel";
            public const string StepConfigureLabel = "StepConfigureLabel";
            public const string StepConfirmLabel = "StepConfirmLabel";

            public const string DuplicateAccountTitle = "DuplicateAccountTitle";
        }
        
        public VaultMnemonicAccountEditorLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }
        
        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "Add Account from Mnemonic",
                [Keys.Description] = "Create additional accounts from existing mnemonics in your vault",
                [Keys.PageTitle] = "Add Account from Existing Mnemonic",
                [Keys.PageDescription] = "Create a new account using an existing mnemonic in your vault",
                [Keys.SelectMnemonicLabel] = "Select Mnemonic",
                [Keys.AccountIndexLabel] = "Account Index",
                [Keys.AccountIndexHelperText] = "Derivation path index for the new account (0, 1, 2, etc.)",
                [Keys.AccountNameLabel] = "Account Name",
                [Keys.AccountNameHelperText] = "Give your account a memorable name",
                [Keys.AccountNamePlaceholder] = "Account 2",
                [Keys.AddressPreviewTitle] = "Address Preview",
                [Keys.CopyAddressTitle] = "Copy Address",
                [Keys.DerivationPathText] = "Derivation path: m/44'/60'/0'/0/{0}",
                [Keys.BackToLoginText] = "Back to Login",
                [Keys.BackToAccountSelectionText] = "Back to Account Selection",
                [Keys.CreateAccountText] = "Create Account",
                [Keys.NoMnemonicsTitle] = "No Mnemonics Available",
                [Keys.NoMnemonicsDescription] = "You need to create at least one mnemonic-based account first before you can derive additional accounts from it.",
                [Keys.ErrorLoadingMnemonics] = "Error loading mnemonics: {0}",
                [Keys.ErrorDerivingAddress] = "Error deriving address: {0}",
                
                [Keys.SelectMnemonicTitle] = "Select Mnemonic",
                [Keys.SelectMnemonicSubtitle] = "Choose which mnemonic to derive from",
                [Keys.ConfigureAccountTitle] = "Configure Account",
                [Keys.ConfigureAccountSubtitle] = "Set account index and name",
                [Keys.ConfirmDetailsTitle] = "Confirm Details",
                [Keys.ConfirmDetailsSubtitle] = "Review and confirm your new account",
                [Keys.AddAccount] = "Add Account",
                [Keys.MnemonicSelection] = "Mnemonic Selection",
                [Keys.AccountConfiguration] = "Account Configuration",
                [Keys.AccountConfirmation] = "Account Confirmation",
                [Keys.Exit] = "Exit",
                [Keys.Back] = "Back",
                [Keys.Continue] = "Continue",
                [Keys.MnemonicRequired] = "Mnemonic selection is required",
                [Keys.AccountIndexRequired] = "Account index must be 0 or greater",
                [Keys.DerivedAccountPreview] = "Derived Account Preview",
                [Keys.AccountType] = "Account Type",
                [Keys.MnemonicDerivedAccount] = "Mnemonic-Derived Account",
                [Keys.UnnamedAccount] = "Unnamed Account",
                [Keys.SecurityInfo] = "Security Information",
                [Keys.MnemonicSecurityNotice] = "This account is derived from your existing mnemonic using the specified derivation path. All accounts from the same mnemonic share the same seed phrase.",
                [Keys.Error] = "Error",
                
                [Keys.StepSelectMnemonicLabel] = "Select Mnemonic",
                [Keys.StepConfigureLabel] = "Configure",
                [Keys.StepConfirmLabel] = "Confirm",

                [Keys.DuplicateAccountTitle] = "Duplicate Account"
            });

            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "Añadir Cuenta desde Mnemónico",
                [Keys.Description] = "Crear cuentas adicionales desde mnemónicos existentes en tu bóveda",
                [Keys.PageTitle] = "Añadir Cuenta desde Mnemónico Existente",
                [Keys.PageDescription] = "Crear una nueva cuenta usando un mnemónico existente en tu bóveda",
                [Keys.SelectMnemonicLabel] = "Seleccionar Mnemónico",
                [Keys.AccountIndexLabel] = "Índice de Cuenta",
                [Keys.AccountIndexHelperText] = "Índice de ruta de derivación para la nueva cuenta (0, 1, 2, etc.)",
                [Keys.AccountNameLabel] = "Nombre de Cuenta",
                [Keys.AccountNameHelperText] = "Dale un nombre memorable a tu cuenta",
                [Keys.AccountNamePlaceholder] = "Cuenta 2",
                [Keys.AddressPreviewTitle] = "Vista Previa de Dirección",
                [Keys.CopyAddressTitle] = "Copiar Dirección",
                [Keys.DerivationPathText] = "Ruta de derivación: m/44'/60'/0'/0/{0}",
                [Keys.BackToLoginText] = "Volver al Inicio de Sesión",
                [Keys.BackToAccountSelectionText] = "Volver a Selección de Cuenta",
                [Keys.CreateAccountText] = "Crear Cuenta",
                [Keys.NoMnemonicsTitle] = "No Hay Mnemónicos Disponibles",
                [Keys.NoMnemonicsDescription] = "Necesitas crear al menos una cuenta basada en mnemónico primero antes de poder derivar cuentas adicionales de ella.",
                [Keys.ErrorLoadingMnemonics] = "Error cargando mnemónicos: {0}",
                [Keys.ErrorDerivingAddress] = "Error derivando dirección: {0}",
                
                [Keys.SelectMnemonicTitle] = "Seleccionar Mnemónico",
                [Keys.SelectMnemonicSubtitle] = "Elige de cuál mnemónico derivar",
                [Keys.ConfigureAccountTitle] = "Configurar Cuenta",
                [Keys.ConfigureAccountSubtitle] = "Establecer índice y nombre de cuenta",
                [Keys.ConfirmDetailsTitle] = "Confirmar Detalles",
                [Keys.ConfirmDetailsSubtitle] = "Revisar y confirmar tu nueva cuenta",
                [Keys.AddAccount] = "Añadir Cuenta",
                [Keys.MnemonicSelection] = "Selección de Mnemónico",
                [Keys.AccountConfiguration] = "Configuración de Cuenta",
                [Keys.AccountConfirmation] = "Confirmación de Cuenta",
                [Keys.Exit] = "Salir",
                [Keys.Back] = "Atrás",
                [Keys.Continue] = "Continuar",
                [Keys.MnemonicRequired] = "Se requiere selección de mnemónico",
                [Keys.AccountIndexRequired] = "El índice de cuenta debe ser 0 o mayor",
                [Keys.DerivedAccountPreview] = "Vista Previa de Cuenta Derivada",
                [Keys.AccountType] = "Tipo de Cuenta",
                [Keys.MnemonicDerivedAccount] = "Cuenta Derivada de Mnemónico",
                [Keys.UnnamedAccount] = "Cuenta Sin Nombre",
                [Keys.SecurityInfo] = "Información de Seguridad",
                [Keys.MnemonicSecurityNotice] = "Esta cuenta se deriva de tu mnemónico existente usando la ruta de derivación especificada. Todas las cuentas del mismo mnemónico comparten la misma frase semilla.",
                [Keys.Error] = "Error",
                
                [Keys.StepSelectMnemonicLabel] = "Seleccionar Mnemónico",
                [Keys.StepConfigureLabel] = "Configurar",
                [Keys.StepConfirmLabel] = "Confirmar",

                [Keys.DuplicateAccountTitle] = "Cuenta Duplicada"
            });
        }
    }
}