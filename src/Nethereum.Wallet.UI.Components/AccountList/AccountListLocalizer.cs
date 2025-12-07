using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.AccountList
{
    public class AccountListLocalizer : ComponentLocalizerBase<AccountListViewModel>
    {
        public static class Keys
        {
            public const string Title = "Title";
            public const string AccountsLabel = "AccountsLabel";
            public const string NoAccountsMessage = "NoAccountsMessage";
            public const string AddAccountButton = "AddAccountButton";
            public const string EditAccountButton = "EditAccountButton";
            public const string DeleteAccountButton = "DeleteAccountButton";
            public const string AccountNameLabel = "AccountNameLabel";
            public const string AccountAddressLabel = "AccountAddressLabel";
            public const string AccountBalanceLabel = "AccountBalanceLabel";
            public const string AccountTypeLabel = "AccountTypeLabel";
            public const string ConfirmDeleteTitle = "ConfirmDeleteTitle";
            public const string ConfirmDeleteMessage = "ConfirmDeleteMessage";
            public const string DeleteButton = "DeleteButton";
            public const string CancelButton = "CancelButton";
            public const string BackToAccounts = "BackToAccounts";
            public const string LoadingAccounts = "LoadingAccounts";
            public const string NoAccountsYet = "NoAccountsYet";
            public const string NoAccountsDescription = "NoAccountsDescription";
            public const string AddFirstAccount = "AddFirstAccount";
            public const string ViewDetails = "ViewDetails";
            public const string ListView = "ListView";
            public const string GroupedView = "GroupedView";
            public const string SelectButton = "SelectButton";
            public const string ManageButton = "ManageButton";
        }

        public AccountListLocalizer(IWalletLocalizationService globalService) : base(globalService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.Title] = "Account List",
                [Keys.AccountsLabel] = "Your Accounts",
                [Keys.NoAccountsMessage] = "No accounts found. Create your first account to get started.",
                [Keys.AddAccountButton] = "Add Account",
                [Keys.EditAccountButton] = "Edit",
                [Keys.DeleteAccountButton] = "Delete",
                [Keys.AccountNameLabel] = "Account Name",
                [Keys.AccountAddressLabel] = "Address",
                [Keys.AccountBalanceLabel] = "Balance",
                [Keys.AccountTypeLabel] = "Type",
                [Keys.ConfirmDeleteTitle] = "Confirm Delete",
                [Keys.ConfirmDeleteMessage] = "Are you sure you want to delete this account? This action cannot be undone.",
                [Keys.DeleteButton] = "Delete",
                [Keys.CancelButton] = "Cancel",
                [Keys.BackToAccounts] = "Back to Accounts",
                [Keys.LoadingAccounts] = "Loading accounts...",
                [Keys.NoAccountsYet] = "No accounts yet",
                [Keys.NoAccountsDescription] = "Create or import your first account to get started with your wallet",
                [Keys.AddFirstAccount] = "Add Your First Account",
                [Keys.ViewDetails] = "View Details",
                [Keys.ListView] = "List",
                [Keys.GroupedView] = "Grouped",
                [Keys.SelectButton] = "Select",
                [Keys.ManageButton] = "Manage"
            });
            
            // Spanish (Spain) translations
            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.Title] = "Lista de Cuentas",
                [Keys.AccountsLabel] = "Tus Cuentas",
                [Keys.NoAccountsMessage] = "No se encontraron cuentas. Crea tu primera cuenta para comenzar.",
                [Keys.AddAccountButton] = "Agregar Cuenta",
                [Keys.EditAccountButton] = "Editar",
                [Keys.DeleteAccountButton] = "Eliminar",
                [Keys.AccountNameLabel] = "Nombre de la Cuenta",
                [Keys.AccountAddressLabel] = "Dirección",
                [Keys.AccountBalanceLabel] = "Saldo",
                [Keys.AccountTypeLabel] = "Tipo",
                [Keys.ConfirmDeleteTitle] = "Confirmar Eliminación",
                [Keys.ConfirmDeleteMessage] = "¿Estás seguro de que quieres eliminar esta cuenta? Esta acción no se puede deshacer.",
                [Keys.DeleteButton] = "Eliminar",
                [Keys.CancelButton] = "Cancelar",
                [Keys.BackToAccounts] = "Volver a Cuentas",
                [Keys.LoadingAccounts] = "Cargando cuentas...",
                [Keys.NoAccountsYet] = "Aún no hay cuentas",
                [Keys.NoAccountsDescription] = "Crea o importa tu primera cuenta para comenzar con tu cartera",
                [Keys.AddFirstAccount] = "Agregar Tu Primera Cuenta",
                [Keys.ViewDetails] = "Ver Detalles",
                [Keys.ListView] = "Lista",
                [Keys.GroupedView] = "Agrupado",
                [Keys.SelectButton] = "Seleccionar",
                [Keys.ManageButton] = "Gestionar"
            });
        }
    }
}