using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.WalletAccounts.ViewOnly
{
    public class ViewOnlyAccountDetailsLocalizer : ComponentLocalizerBase<ViewOnlyAccountDetailsViewModel>
    {
        public static class Keys
        {
            public const string BackToAccounts = "BackToAccounts";
            public const string Back = "Back";
            public const string Continue = "Continue";
            
            public const string AccountDetails = "AccountDetails";
            public const string EditAccountName = "EditAccountName";
            public const string AccountInfo = "AccountInfo";
            public const string RemoveAccount = "RemoveAccount";
            public const string ViewInfo = "ViewInfo";
            
            public const string AccountDescription = "AccountDescription";
            public const string UnnamedAccount = "UnnamedAccount";
            public const string ViewOnlyBadge = "ViewOnlyBadge";
            public const string ViewOnlyAccountType = "ViewOnlyAccountType";
            
            public const string AccountNameLabel = "AccountNameLabel";
            public const string AccountNamePlaceholder = "AccountNamePlaceholder";
            public const string AccountNameHelperText = "AccountNameHelperText";
            public const string AccountNameRequired = "AccountNameRequired";
            public const string ChangeAccountName = "ChangeAccountName";
            public const string SaveChanges = "SaveChanges";
            public const string AccountNameUpdated = "AccountNameUpdated";
            
            public const string ViewOnlyCapabilities = "ViewOnlyCapabilities";
            public const string ViewOnlySecurityNotice = "ViewOnlySecurityNotice";
            public const string WhatYouCanDoTitle = "WhatYouCanDoTitle";
            public const string WhatYouCannotDoTitle = "WhatYouCannotDoTitle";
            public const string ViewBalances = "ViewBalances";
            public const string ViewTransactions = "ViewTransactions";
            public const string ViewNFTs = "ViewNFTs";
            public const string TrackPortfolio = "TrackPortfolio";
            public const string CannotSend = "CannotSend";
            public const string CannotSign = "CannotSign";
            public const string CannotInteract = "CannotInteract";
            
            public const string ConfirmRemoval = "ConfirmRemoval";
            public const string ConfirmRemovalMessage = "ConfirmRemovalMessage";
            public const string AccountRemoved = "AccountRemoved";
            public const string RemovalError = "RemovalError";
            public const string CannotRemoveLastAccount = "CannotRemoveLastAccount";
            public const string Cancel = "Cancel";
            
            public const string LoadingAccount = "LoadingAccount";
            public const string NoAccountSelected = "NoAccountSelected";
            public const string SelectAccountMessage = "SelectAccountMessage";
            public const string Error = "Error";
            public const string Success = "Success";
        }
        
        public ViewOnlyAccountDetailsLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }
        
        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.BackToAccounts] = "Back to Accounts",
                [Keys.Back] = "Back",
                [Keys.Continue] = "Continue",
                
                [Keys.AccountDetails] = "Account Details",
                [Keys.EditAccountName] = "Edit Name",
                [Keys.AccountInfo] = "Account Information",
                [Keys.RemoveAccount] = "Remove Account",
                [Keys.ViewInfo] = "View Info",
                
                [Keys.AccountDescription] = "{0} is a view-only account for tracking balances and transactions",
                [Keys.UnnamedAccount] = "Unnamed Account",
                [Keys.ViewOnlyBadge] = "View-Only Account",
                [Keys.ViewOnlyAccountType] = "View-Only / Watch Account",
                
                [Keys.AccountNameLabel] = "Account Name",
                [Keys.AccountNamePlaceholder] = "Enter account name",
                [Keys.AccountNameHelperText] = "Give your account a memorable name",
                [Keys.AccountNameRequired] = "Account name is required",
                [Keys.ChangeAccountName] = "Change the name of this account",
                [Keys.SaveChanges] = "Save Changes",
                [Keys.AccountNameUpdated] = "Account name updated successfully",
                
                [Keys.ViewOnlyCapabilities] = "View-Only Account Capabilities",
                [Keys.ViewOnlySecurityNotice] = "This account allows you to view balances and transactions but cannot sign or send transactions.",
                [Keys.WhatYouCanDoTitle] = "What You Can Do",
                [Keys.WhatYouCannotDoTitle] = "What You Cannot Do",
                [Keys.ViewBalances] = "View token balances and value",
                [Keys.ViewTransactions] = "View transaction history",
                [Keys.ViewNFTs] = "View NFTs and collectibles",
                [Keys.TrackPortfolio] = "Track portfolio performance",
                [Keys.CannotSend] = "Cannot send transactions",
                [Keys.CannotSign] = "Cannot sign messages",
                [Keys.CannotInteract] = "Cannot interact with smart contracts",
                
                [Keys.ConfirmRemoval] = "Remove Account",
                [Keys.ConfirmRemovalMessage] = "Are you sure you want to remove '{0}'? This action cannot be undone.",
                [Keys.AccountRemoved] = "Account removed successfully",
                [Keys.RemovalError] = "Failed to remove account: {0}",
                [Keys.CannotRemoveLastAccount] = "Cannot remove the last account",
                [Keys.Cancel] = "Cancel",
                
                [Keys.LoadingAccount] = "Loading account details...",
                [Keys.NoAccountSelected] = "No Account Selected",
                [Keys.SelectAccountMessage] = "Please select an account to view its details",
                [Keys.Error] = "Error",
                [Keys.Success] = "Success"
            });
            
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.BackToAccounts] = "Volver a Cuentas",
                [Keys.Back] = "Atrás",
                [Keys.Continue] = "Continuar",
                
                [Keys.AccountDetails] = "Detalles de Cuenta",
                [Keys.EditAccountName] = "Editar Nombre",
                [Keys.AccountInfo] = "Información de Cuenta",
                [Keys.RemoveAccount] = "Eliminar Cuenta",
                [Keys.ViewInfo] = "Ver Información",
                
                [Keys.AccountDescription] = "{0} es una cuenta de solo lectura para rastrear saldos y transacciones",
                [Keys.UnnamedAccount] = "Cuenta Sin Nombre",
                [Keys.ViewOnlyBadge] = "Cuenta de Solo Lectura",
                [Keys.ViewOnlyAccountType] = "Solo Lectura / Cuenta de Observación",
                
                [Keys.AccountNameLabel] = "Nombre de Cuenta",
                [Keys.AccountNamePlaceholder] = "Ingresa el nombre de la cuenta",
                [Keys.AccountNameHelperText] = "Dale un nombre memorable a tu cuenta",
                [Keys.AccountNameRequired] = "El nombre de la cuenta es requerido",
                [Keys.ChangeAccountName] = "Cambiar el nombre de esta cuenta",
                [Keys.SaveChanges] = "Guardar Cambios",
                [Keys.AccountNameUpdated] = "Nombre de cuenta actualizado exitosamente",
                
                [Keys.ViewOnlyCapabilities] = "Capacidades de Cuenta de Solo Lectura",
                [Keys.ViewOnlySecurityNotice] = "Esta cuenta te permite ver saldos y transacciones pero no puede firmar o enviar transacciones.",
                [Keys.WhatYouCanDoTitle] = "Lo Que Puedes Hacer",
                [Keys.WhatYouCannotDoTitle] = "Lo Que No Puedes Hacer",
                [Keys.ViewBalances] = "Ver saldos y valor de tokens",
                [Keys.ViewTransactions] = "Ver historial de transacciones",
                [Keys.ViewNFTs] = "Ver NFTs y coleccionables",
                [Keys.TrackPortfolio] = "Rastrear rendimiento del portafolio",
                [Keys.CannotSend] = "No puede enviar transacciones",
                [Keys.CannotSign] = "No puede firmar mensajes",
                [Keys.CannotInteract] = "No puede interactuar con contratos inteligentes",
                
                [Keys.ConfirmRemoval] = "Eliminar Cuenta",
                [Keys.ConfirmRemovalMessage] = "¿Estás seguro de que quieres eliminar '{0}'? Esta acción no se puede deshacer.",
                [Keys.AccountRemoved] = "Cuenta eliminada exitosamente",
                [Keys.RemovalError] = "Error al eliminar cuenta: {0}",
                [Keys.CannotRemoveLastAccount] = "No se puede eliminar la última cuenta",
                [Keys.Cancel] = "Cancelar",
                
                [Keys.LoadingAccount] = "Cargando detalles de cuenta...",
                [Keys.NoAccountSelected] = "Ninguna Cuenta Seleccionada",
                [Keys.SelectAccountMessage] = "Por favor selecciona una cuenta para ver sus detalles",
                [Keys.Error] = "Error",
                [Keys.Success] = "Éxito"
            });
        }
    }
}