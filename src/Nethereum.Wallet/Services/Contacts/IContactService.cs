using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Wallet.Services.Contacts
{
    public interface IContactService
    {
        Task<List<Contact>> GetAllContactsAsync();
        Task<Contact> GetContactByAddressAsync(string address);
        Task AddContactAsync(Contact contact);
        Task UpdateContactAsync(Contact contact);
        Task DeleteContactAsync(string address);
        Task<bool> ExistsAsync(string address);
        Task<List<Contact>> SearchAsync(string searchText);

        event EventHandler<ContactChangedEventArgs> ContactChanged;
    }

    public class Contact
    {
        public string Address { get; set; }
        public string Name { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public string FormattedAddress =>
            !string.IsNullOrEmpty(Address) && Address.Length > 10
                ? $"{Address.Substring(0, 6)}...{Address.Substring(Address.Length - 4)}"
                : Address;
    }

    public class ContactChangedEventArgs : EventArgs
    {
        public Contact Contact { get; set; }
        public ContactChangeType ChangeType { get; set; }
    }

    public enum ContactChangeType
    {
        Added,
        Updated,
        Deleted
    }
}
