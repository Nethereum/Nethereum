using System.Collections.Generic;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Contacts
{
    public class ContactListLocalizer : ComponentLocalizerBase<ContactListViewModel>
    {
        public static class Keys
        {
            public const string Title = "Title";
            public const string PluginName = "PluginName";
            public const string PluginDescription = "PluginDescription";
            public const string AddContact = "AddContact";
            public const string EditContact = "EditContact";
            public const string DeleteContact = "DeleteContact";
            public const string Search = "Search";
            public const string SearchPlaceholder = "SearchPlaceholder";
            public const string Name = "Name";
            public const string Address = "Address";
            public const string Notes = "Notes";
            public const string Save = "Save";
            public const string Cancel = "Cancel";
            public const string Delete = "Delete";
            public const string Edit = "Edit";
            public const string Select = "Select";
            public const string NoContacts = "NoContacts";
            public const string NoContactsDescription = "NoContactsDescription";
            public const string NameRequired = "NameRequired";
            public const string AddressRequired = "AddressRequired";
            public const string InvalidAddress = "InvalidAddress";
            public const string AddressAlreadyExists = "AddressAlreadyExists";
            public const string ContactAdded = "ContactAdded";
            public const string ContactUpdated = "ContactUpdated";
            public const string ContactDeleted = "ContactDeleted";
            public const string ConfirmDelete = "ConfirmDelete";
            public const string ConfirmDeleteMessage = "ConfirmDeleteMessage";
            public const string Loading = "Loading";
            public const string SelectContact = "SelectContact";
            public const string NoMatchingContacts = "NoMatchingContacts";
            public const string YourAccounts = "YourAccounts";
            public const string SavedContacts = "SavedContacts";
        }

        public ContactListLocalizer(IWalletLocalizationService localizationService)
            : base(localizationService)
        {
        }

        protected override void RegisterTranslations()
        {
            _globalService.RegisterTranslations(_componentName, "en-US", new Dictionary<string, string>
            {
                [Keys.Title] = "Contacts",
                [Keys.PluginName] = "Contacts",
                [Keys.PluginDescription] = "Manage your saved addresses",
                [Keys.AddContact] = "Add Contact",
                [Keys.EditContact] = "Edit Contact",
                [Keys.DeleteContact] = "Delete Contact",
                [Keys.Search] = "Search",
                [Keys.SearchPlaceholder] = "Search contacts...",
                [Keys.Name] = "Name",
                [Keys.Address] = "Address",
                [Keys.Notes] = "Notes (optional)",
                [Keys.Save] = "Save",
                [Keys.Cancel] = "Cancel",
                [Keys.Delete] = "Delete",
                [Keys.Edit] = "Edit",
                [Keys.Select] = "Select",
                [Keys.NoContacts] = "No Contacts Yet",
                [Keys.NoContactsDescription] = "Add contacts to save frequently used addresses",
                [Keys.NameRequired] = "Name is required",
                [Keys.AddressRequired] = "Address is required",
                [Keys.InvalidAddress] = "Invalid Ethereum address",
                [Keys.AddressAlreadyExists] = "A contact with this address already exists",
                [Keys.ContactAdded] = "Contact added",
                [Keys.ContactUpdated] = "Contact updated",
                [Keys.ContactDeleted] = "Contact deleted",
                [Keys.ConfirmDelete] = "Delete Contact?",
                [Keys.ConfirmDeleteMessage] = "Are you sure you want to delete this contact?",
                [Keys.Loading] = "Loading contacts...",
                [Keys.SelectContact] = "Select Contact",
                [Keys.NoMatchingContacts] = "No matching contacts",
                [Keys.YourAccounts] = "Your Accounts",
                [Keys.SavedContacts] = "Saved Contacts"
            });

            _globalService.RegisterTranslations(_componentName, "es-ES", new Dictionary<string, string>
            {
                [Keys.Title] = "Contactos",
                [Keys.PluginName] = "Contactos",
                [Keys.PluginDescription] = "Administra tus direcciones guardadas",
                [Keys.AddContact] = "Agregar Contacto",
                [Keys.EditContact] = "Editar Contacto",
                [Keys.DeleteContact] = "Eliminar Contacto",
                [Keys.Search] = "Buscar",
                [Keys.SearchPlaceholder] = "Buscar contactos...",
                [Keys.Name] = "Nombre",
                [Keys.Address] = "Dirección",
                [Keys.Notes] = "Notas (opcional)",
                [Keys.Save] = "Guardar",
                [Keys.Cancel] = "Cancelar",
                [Keys.Delete] = "Eliminar",
                [Keys.Edit] = "Editar",
                [Keys.Select] = "Seleccionar",
                [Keys.NoContacts] = "Sin Contactos",
                [Keys.NoContactsDescription] = "Agrega contactos para guardar direcciones frecuentes",
                [Keys.NameRequired] = "El nombre es requerido",
                [Keys.AddressRequired] = "La dirección es requerida",
                [Keys.InvalidAddress] = "Dirección Ethereum inválida",
                [Keys.AddressAlreadyExists] = "Ya existe un contacto con esta dirección",
                [Keys.ContactAdded] = "Contacto agregado",
                [Keys.ContactUpdated] = "Contacto actualizado",
                [Keys.ContactDeleted] = "Contacto eliminado",
                [Keys.ConfirmDelete] = "¿Eliminar Contacto?",
                [Keys.ConfirmDeleteMessage] = "¿Estás seguro de que deseas eliminar este contacto?",
                [Keys.Loading] = "Cargando contactos...",
                [Keys.SelectContact] = "Seleccionar Contacto",
                [Keys.NoMatchingContacts] = "No hay contactos coincidentes",
                [Keys.YourAccounts] = "Tus Cuentas",
                [Keys.SavedContacts] = "Contactos Guardados"
            });
        }
    }
}
