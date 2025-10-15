using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.ViewOnly
{
    public class ViewOnlyAccountEditorLocalizer : ComponentLocalizerBase<ViewOnlyAccountCreationViewModel>
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
            public const string WhatCanDoTitle = "WhatCanDoTitle";
            public const string ViewBalanceText = "ViewBalanceText";
            public const string ViewTransactionsText = "ViewTransactionsText";
            public const string ViewNFTsText = "ViewNFTsText";
            public const string WhatCannotDoTitle = "WhatCannotDoTitle";
            public const string CannotSendText = "CannotSendText";
            public const string CannotSignText = "CannotSignText";
            public const string CannotInteractText = "CannotInteractText";
            public const string BackToLoginText = "BackToLoginText";
            public const string BackToAccountSelectionText = "BackToAccountSelectionText";
            public const string AddAccountText = "AddAccountText";
            public const string ValidAddressMessage = "ValidAddressMessage";
            public const string InvalidAddressMessage = "InvalidAddressMessage";
            
            public const string InvalidEthereumAddressError = "InvalidEthereumAddressError";
            public const string CreateAccountError = "CreateAccountError";
            
            public const string SetupAccountTitle = "SetupAccountTitle";
            public const string SetupAccountSubtitle = "SetupAccountSubtitle";
            public const string EnterAddressTitle = "EnterAddressTitle";
            public const string EnterAddressSubtitle = "EnterAddressSubtitle";
            public const string ConfirmDetailsTitle = "ConfirmDetailsTitle";
            public const string ConfirmDetailsSubtitle = "ConfirmDetailsSubtitle";
            public const string AddViewOnlyAccount = "AddViewOnlyAccount";
            public const string ViewOnlyAccountTitle = "ViewOnlyAccountTitle";
            public const string ViewOnlyAccountDescription = "ViewOnlyAccountDescription";
            public const string ValidAddressDescription = "ValidAddressDescription";
            public const string WhatYouCanDoTitle = "WhatYouCanDoTitle";
            public const string TrackPortfolioText = "TrackPortfolioText";
            public const string UnnamedAccount = "UnnamedAccount";
            public const string AccountTypeLabel = "AccountTypeLabel";
            public const string ViewOnlyAccountType = "ViewOnlyAccountType";
            public const string SecurityNoticeTitle = "SecurityNoticeTitle";
            public const string ViewOnlySecurityNotice = "ViewOnlySecurityNotice";
            public const string AddressRequired = "AddressRequired";
            public const string Error = "Error";
            public const string Exit = "Exit";
            public const string BackButtonText = "BackButtonText";
            public const string ContinueButtonText = "ContinueButtonText";
            
            public const string StepSetupLabel = "StepSetupLabel";
            public const string StepAddressLabel = "StepAddressLabel";
            public const string StepConfirmLabel = "StepConfirmLabel";
        }
        
        public ViewOnlyAccountEditorLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }
        
        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "View-Only Account",
                [Keys.Description] = "Watch any Ethereum address without the ability to send transactions",
                [Keys.AccountNameLabel] = "Account Name",
                [Keys.AccountNameHelperText] = "Give your account a memorable name (optional)",
                [Keys.AccountNamePlaceholder] = "Watch Account",
                [Keys.AddressLabel] = "Ethereum Address",
                [Keys.AddressHelperText] = "Enter the Ethereum address you want to watch",
                [Keys.AddressPlaceholder] = "0x...",
                [Keys.ValidAddressText] = "Valid Address",
                [Keys.InvalidAddressText] = "Invalid Address",
                [Keys.InfoTitle] = "View-Only Account",
                [Keys.InfoDescription] = "This account can only view balances and transactions. You cannot send transactions or sign messages.",
                [Keys.WhatCanDoTitle] = "What you can do:",
                [Keys.ViewBalanceText] = "• View token balances",
                [Keys.ViewTransactionsText] = "• View transaction history",
                [Keys.ViewNFTsText] = "• View NFTs and collectibles",
                [Keys.WhatCannotDoTitle] = "What you cannot do:",
                [Keys.CannotSendText] = "• Send transactions",
                [Keys.CannotSignText] = "• Sign messages",
                [Keys.CannotInteractText] = "• Interact with smart contracts",
                [Keys.BackToLoginText] = "Back to Login",
                [Keys.BackToAccountSelectionText] = "Back to Account Selection",
                [Keys.AddAccountText] = "Add Account",
                [Keys.ValidAddressMessage] = "Valid Ethereum address",
                [Keys.InvalidAddressMessage] = "Invalid Ethereum address format",
                
                [Keys.InvalidEthereumAddressError] = "Please enter a valid Ethereum address",
                [Keys.CreateAccountError] = "Error creating account: {0}",
                
                [Keys.SetupAccountTitle] = "Account Name",
                [Keys.SetupAccountSubtitle] = "Give your view-only account a name",
                [Keys.EnterAddressTitle] = "Enter Address",
                [Keys.EnterAddressSubtitle] = "Enter the Ethereum address to watch",
                [Keys.ConfirmDetailsTitle] = "Confirm Details",
                [Keys.ConfirmDetailsSubtitle] = "Review and confirm your view-only account",
                [Keys.AddViewOnlyAccount] = "Add View-Only Account",
                [Keys.ViewOnlyAccountTitle] = "View-Only Account",
                [Keys.ViewOnlyAccountDescription] = "Track balances and transactions without the ability to send funds or sign messages",
                [Keys.ValidAddressDescription] = "This is a valid Ethereum address",
                [Keys.WhatYouCanDoTitle] = "What You Can Do",
                [Keys.TrackPortfolioText] = "Track portfolio value",
                [Keys.UnnamedAccount] = "Unnamed Account",
                [Keys.AccountTypeLabel] = "Account Type",
                [Keys.ViewOnlyAccountType] = "View-Only",
                [Keys.SecurityNoticeTitle] = "Security Notice",
                [Keys.ViewOnlySecurityNotice] = "This account cannot send transactions or sign messages. It's safe to share the address publicly.",
                [Keys.AddressRequired] = "Address is required",
                [Keys.Error] = "Error",
                [Keys.Exit] = "Exit",
                [Keys.BackButtonText] = "Back",
                [Keys.ContinueButtonText] = "Continue",
                
                [Keys.StepSetupLabel] = "Setup",
                [Keys.StepAddressLabel] = "Address",
                [Keys.StepConfirmLabel] = "Confirm"
            });
            
            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.DisplayName] = "Cuenta de Solo Lectura",
                [Keys.Description] = "Observar cualquier dirección Ethereum sin la capacidad de enviar transacciones",
                [Keys.AccountNameLabel] = "Nombre de Cuenta",
                [Keys.AccountNameHelperText] = "Dale un nombre memorable a tu cuenta (opcional)",
                [Keys.AccountNamePlaceholder] = "Cuenta de Observación",
                [Keys.AddressLabel] = "Dirección Ethereum",
                [Keys.AddressHelperText] = "Ingresa la dirección Ethereum que quieres observar",
                [Keys.AddressPlaceholder] = "0x...",
                [Keys.ValidAddressText] = "Dirección Válida",
                [Keys.InvalidAddressText] = "Dirección Inválida",
                [Keys.InfoTitle] = "Cuenta de Solo Lectura",
                [Keys.InfoDescription] = "Esta cuenta solo puede ver saldos y transacciones. No puedes enviar transacciones o firmar mensajes.",
                [Keys.WhatCanDoTitle] = "Lo que puedes hacer:",
                [Keys.ViewBalanceText] = "• Ver saldos de tokens",
                [Keys.ViewTransactionsText] = "• Ver historial de transacciones",
                [Keys.ViewNFTsText] = "• Ver NFTs y coleccionables",
                [Keys.WhatCannotDoTitle] = "Lo que no puedes hacer:",
                [Keys.CannotSendText] = "• Enviar transacciones",
                [Keys.CannotSignText] = "• Firmar mensajes",
                [Keys.CannotInteractText] = "• Interactuar con contratos inteligentes",
                [Keys.BackToLoginText] = "Volver al Inicio de Sesión",
                [Keys.BackToAccountSelectionText] = "Volver a Selección de Cuenta",
                [Keys.AddAccountText] = "Añadir Cuenta",
                [Keys.ValidAddressMessage] = "Dirección Ethereum válida",
                [Keys.InvalidAddressMessage] = "Formato de dirección Ethereum inválido",
                
                [Keys.InvalidEthereumAddressError] = "Por favor ingresa una dirección Ethereum válida",
                [Keys.CreateAccountError] = "Error creando cuenta: {0}",
                
                [Keys.SetupAccountTitle] = "Nombre de Cuenta",
                [Keys.SetupAccountSubtitle] = "Dale un nombre a tu cuenta de solo lectura",
                [Keys.EnterAddressTitle] = "Ingresar Dirección",
                [Keys.EnterAddressSubtitle] = "Ingresa la dirección Ethereum a observar",
                [Keys.ConfirmDetailsTitle] = "Confirmar Detalles",
                [Keys.ConfirmDetailsSubtitle] = "Revisa y confirma tu cuenta de solo lectura",
                [Keys.AddViewOnlyAccount] = "Añadir Cuenta de Solo Lectura",
                [Keys.ViewOnlyAccountTitle] = "Cuenta de Solo Lectura",
                [Keys.ViewOnlyAccountDescription] = "Rastrea saldos y transacciones sin la capacidad de enviar fondos o firmar mensajes",
                [Keys.ValidAddressDescription] = "Esta es una dirección Ethereum válida",
                [Keys.WhatYouCanDoTitle] = "Lo Que Puedes Hacer",
                [Keys.TrackPortfolioText] = "Rastrear valor del portafolio",
                [Keys.UnnamedAccount] = "Cuenta Sin Nombre",
                [Keys.AccountTypeLabel] = "Tipo de Cuenta",
                [Keys.ViewOnlyAccountType] = "Solo Lectura",
                [Keys.SecurityNoticeTitle] = "Aviso de Seguridad",
                [Keys.ViewOnlySecurityNotice] = "Esta cuenta no puede enviar transacciones o firmar mensajes. Es seguro compartir la dirección públicamente.",
                [Keys.AddressRequired] = "La dirección es requerida",
                [Keys.Error] = "Error",
                [Keys.Exit] = "Salir",
                [Keys.BackButtonText] = "Atrás",
                [Keys.ContinueButtonText] = "Continuar",
                
                [Keys.StepSetupLabel] = "Configurar",
                [Keys.StepAddressLabel] = "Dirección",
                [Keys.StepConfirmLabel] = "Confirmar"
            });
        }
    }
}