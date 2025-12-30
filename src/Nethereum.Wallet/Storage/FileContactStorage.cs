using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.TokenServices.Caching;
using Nethereum.Wallet.Services.Contacts;

namespace Nethereum.Wallet.Storage
{
    public class FileContactStorage : FileStorageBase, IContactService
    {
        private const string ContactsFileName = "contacts.json";
        private List<Contact> _cachedContacts;

        public event EventHandler<ContactChangedEventArgs> ContactChanged;

        public FileContactStorage(
            string baseDirectory = null,
            JsonSerializerOptions jsonOptions = null,
            Action<string, Exception> onError = null)
            : base(
                baseDirectory ?? GetDefaultDirectory(),
                jsonOptions ?? CreateDefaultJsonOptions(),
                onError)
        {
        }

        private static string GetDefaultDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Nethereum", "Wallet", "contacts");
        }

        public async Task<List<Contact>> GetAllContactsAsync()
        {
            if (_cachedContacts != null)
                return _cachedContacts.ToList();

            var data = await ReadAsync<ContactsData>(ContactsFileName).ConfigureAwait(false);
            _cachedContacts = data?.Contacts ?? new List<Contact>();
            return _cachedContacts.ToList();
        }

        public async Task<Contact> GetContactByAddressAsync(string address)
        {
            if (string.IsNullOrEmpty(address))
                return null;

            var contacts = await GetAllContactsAsync().ConfigureAwait(false);
            return contacts.FirstOrDefault(c =>
                string.Equals(c.Address, address, StringComparison.OrdinalIgnoreCase));
        }

        public async Task AddContactAsync(Contact contact)
        {
            if (contact == null)
                throw new ArgumentNullException(nameof(contact));
            if (string.IsNullOrEmpty(contact.Address))
                throw new ArgumentException("Contact address is required", nameof(contact));

            var contacts = await GetAllContactsAsync().ConfigureAwait(false);

            if (contacts.Any(c => string.Equals(c.Address, contact.Address, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Contact with address {contact.Address} already exists");

            contact.CreatedAt = DateTime.UtcNow;
            contacts.Add(contact);
            _cachedContacts = contacts;

            await SaveContactsAsync(contacts).ConfigureAwait(false);

            ContactChanged?.Invoke(this, new ContactChangedEventArgs
            {
                Contact = contact,
                ChangeType = ContactChangeType.Added
            });
        }

        public async Task UpdateContactAsync(Contact contact)
        {
            if (contact == null)
                throw new ArgumentNullException(nameof(contact));
            if (string.IsNullOrEmpty(contact.Address))
                throw new ArgumentException("Contact address is required", nameof(contact));

            var contacts = await GetAllContactsAsync().ConfigureAwait(false);
            var existingIndex = contacts.FindIndex(c =>
                string.Equals(c.Address, contact.Address, StringComparison.OrdinalIgnoreCase));

            if (existingIndex < 0)
                throw new InvalidOperationException($"Contact with address {contact.Address} not found");

            contact.UpdatedAt = DateTime.UtcNow;
            contact.CreatedAt = contacts[existingIndex].CreatedAt;
            contacts[existingIndex] = contact;
            _cachedContacts = contacts;

            await SaveContactsAsync(contacts).ConfigureAwait(false);

            ContactChanged?.Invoke(this, new ContactChangedEventArgs
            {
                Contact = contact,
                ChangeType = ContactChangeType.Updated
            });
        }

        public async Task DeleteContactAsync(string address)
        {
            if (string.IsNullOrEmpty(address))
                throw new ArgumentException("Address is required", nameof(address));

            var contacts = await GetAllContactsAsync().ConfigureAwait(false);
            var contact = contacts.FirstOrDefault(c =>
                string.Equals(c.Address, address, StringComparison.OrdinalIgnoreCase));

            if (contact == null)
                return;

            contacts.Remove(contact);
            _cachedContacts = contacts;

            await SaveContactsAsync(contacts).ConfigureAwait(false);

            ContactChanged?.Invoke(this, new ContactChangedEventArgs
            {
                Contact = contact,
                ChangeType = ContactChangeType.Deleted
            });
        }

        public async Task<bool> ExistsAsync(string address)
        {
            if (string.IsNullOrEmpty(address))
                return false;

            var contacts = await GetAllContactsAsync().ConfigureAwait(false);
            return contacts.Any(c =>
                string.Equals(c.Address, address, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<List<Contact>> SearchAsync(string searchText)
        {
            var contacts = await GetAllContactsAsync().ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(searchText))
                return contacts;

            var search = searchText.ToLowerInvariant();
            return contacts
                .Where(c =>
                    (c.Name?.ToLowerInvariant().Contains(search) ?? false) ||
                    (c.Address?.ToLowerInvariant().Contains(search) ?? false) ||
                    (c.Notes?.ToLowerInvariant().Contains(search) ?? false))
                .ToList();
        }

        private Task SaveContactsAsync(List<Contact> contacts)
        {
            return WriteAsync(ContactsFileName, new ContactsData { Contacts = contacts });
        }

        private class ContactsData
        {
            public List<Contact> Contacts { get; set; } = new List<Contact>();
        }
    }
}
