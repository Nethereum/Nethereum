using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.SmartContract
{
    public class SmartContractAccountEditorLocalizer : ComponentLocalizerBase<SmartContractAccountCreationViewModel>
    {
        public static class Keys
        {
            public const string DisplayName = "DisplayName";
            public const string Description = "Description";
            public const string AccountNameLabel = "AccountNameLabel";
            public const string AccountNameHelperText = "AccountNameHelperText";
            public const string AccountNamePlaceholder = "AccountNamePlaceholder";
            public const string AddressLabel = "AddressLabel";
            public const string AddressHelperText = "AddressHelperText";
            public const string AddressPlaceholder = "AddressPlaceholder";
            public const string ValidAddressText = "ValidAddressText";
            public const string InvalidAddressText = "InvalidAddressText";
            public const string InfoTitle = "InfoTitle";
            public const string InfoDescription = "InfoDescription";
            public const string RequirementsTitle = "RequirementsTitle";
            public const string ValidContractText = "ValidContractText";
            public const string SupportedStandardText = "SupportedStandardText";
            public const string OwnershipText = "OwnershipText";
            public const string FeaturesTitle = "FeaturesTitle";
            public const string MultiSigText = "MultiSigText";
            public const string GasAbstractionText = "GasAbstractionText";
            public const string RecoveryText = "RecoveryText";
            public const string AdvancedText = "AdvancedText";
            public const string WarningTitle = "WarningTitle";
            public const string VerifyOwnershipText = "VerifyOwnershipText";
            public const string TestSmallAmountText = "TestSmallAmountText";
            public const string BackToLoginText = "BackToLoginText";
            public const string BackToAccountSelectionText = "BackToAccountSelectionText";
            public const string AddAccountText = "AddAccountText";
            public const string ValidContractMessage = "ValidContractMessage";
            public const string InvalidContractMessage = "InvalidContractMessage";
            
            public const string InvalidContractAddressError = "InvalidContractAddressError";
            public const string CreateAccountError = "CreateAccountError";
        }
        
        public SmartContractAccountEditorLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }
        
        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "Smart Contract",
                [Keys.Description] = "Account Abstraction smart contract wallet",
                [Keys.AccountNameLabel] = "Account Name",
                [Keys.AccountNameHelperText] = "Give your account a memorable name (optional)",
                [Keys.AccountNamePlaceholder] = "Smart Contract Account",
                [Keys.AddressLabel] = "Contract Address",
                [Keys.AddressHelperText] = "Enter the smart contract address",
                [Keys.AddressPlaceholder] = "0x...",
                [Keys.ValidAddressText] = "Valid Contract",
                [Keys.InvalidAddressText] = "Invalid Contract",
                [Keys.InfoTitle] = "Smart Contract Account",
                [Keys.InfoDescription] = "Connect to a smart contract wallet like Safe, Argent, or other account abstraction wallets.",
                [Keys.RequirementsTitle] = "Requirements:",
                [Keys.ValidContractText] = "• Must be a valid contract address",
                [Keys.SupportedStandardText] = "• Should follow ERC-4337 or similar standards",
                [Keys.OwnershipText] = "• You must have ownership or signing rights",
                [Keys.FeaturesTitle] = "Features:",
                [Keys.MultiSigText] = "• Multi-signature support",
                [Keys.GasAbstractionText] = "• Gas abstraction capabilities", 
                [Keys.RecoveryText] = "• Social recovery options",
                [Keys.AdvancedText] = "• Advanced transaction batching",
                [Keys.WarningTitle] = "Important:",
                [Keys.VerifyOwnershipText] = "• Verify you have access before adding",
                [Keys.TestSmallAmountText] = "• Test with small amounts first",
                [Keys.BackToLoginText] = "Back to Login",
                [Keys.BackToAccountSelectionText] = "Back to Account Selection",
                [Keys.AddAccountText] = "Add Account",
                [Keys.ValidContractMessage] = "Valid contract address",
                [Keys.InvalidContractMessage] = "Invalid contract address format",
                
                [Keys.InvalidContractAddressError] = "Please enter a valid contract address",
                [Keys.CreateAccountError] = "Error creating account: {0}"
            });
            
            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "Contrato Inteligente",
                [Keys.Description] = "Cartera de contrato inteligente de abstracción de cuenta",
                [Keys.AccountNameLabel] = "Nombre de Cuenta",
                [Keys.AccountNameHelperText] = "Dale un nombre memorable a tu cuenta (opcional)",
                [Keys.AccountNamePlaceholder] = "Cuenta de Contrato Inteligente",
                [Keys.AddressLabel] = "Dirección del Contrato",
                [Keys.AddressHelperText] = "Ingresa la dirección del contrato inteligente",
                [Keys.AddressPlaceholder] = "0x...",
                [Keys.ValidAddressText] = "Contrato Válido",
                [Keys.InvalidAddressText] = "Contrato Inválido",
                [Keys.InfoTitle] = "Cuenta de Contrato Inteligente",
                [Keys.InfoDescription] = "Conecta a una cartera de contrato inteligente como Safe, Argent, u otras carteras de abstracción de cuenta.",
                [Keys.RequirementsTitle] = "Requisitos:",
                [Keys.ValidContractText] = "• Debe ser una dirección de contrato válida",
                [Keys.SupportedStandardText] = "• Debe seguir estándares ERC-4337 o similares",
                [Keys.OwnershipText] = "• Debes tener propiedad o derechos de firma",
                [Keys.FeaturesTitle] = "Características:",
                [Keys.MultiSigText] = "• Soporte multi-firma",
                [Keys.GasAbstractionText] = "• Capacidades de abstracción de gas",
                [Keys.RecoveryText] = "• Opciones de recuperación social",
                [Keys.AdvancedText] = "• Agrupación avanzada de transacciones",
                [Keys.WarningTitle] = "Importante:",
                [Keys.VerifyOwnershipText] = "• Verifica que tienes acceso antes de añadir",
                [Keys.TestSmallAmountText] = "• Prueba con cantidades pequeñas primero",
                [Keys.BackToLoginText] = "Volver al Inicio de Sesión",
                [Keys.BackToAccountSelectionText] = "Volver a Selección de Cuenta",
                [Keys.AddAccountText] = "Añadir Cuenta",
                [Keys.ValidContractMessage] = "Dirección de contrato válida",
                [Keys.InvalidContractMessage] = "Formato de dirección de contrato inválido",
                
                [Keys.InvalidContractAddressError] = "Por favor ingresa una dirección de contrato válida",
                [Keys.CreateAccountError] = "Error creando cuenta: {0}"
            });
        }
    }
}