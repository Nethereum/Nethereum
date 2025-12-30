using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.Services.Contacts;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Contacts
{
    public partial class ContactListViewModel : ObservableObject, IDisposable
    {
        private readonly IContactService _contactService;
        private readonly IComponentLocalizer<ContactListViewModel> _localizer;
        private readonly NethereumWalletHostProvider _walletHostProvider;
        private bool _disposed;

        [ObservableProperty] private ObservableCollection<ContactItemViewModel> _contacts = new();
        [ObservableProperty] private ObservableCollection<ContactItemViewModel> _userAccounts = new();
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _errorMessage;
        [ObservableProperty] private string _searchText;

        [ObservableProperty] private bool _isEditMode;
        [ObservableProperty] private ContactItemViewModel _selectedContact;
        [ObservableProperty] private bool _isShowingAddEdit;

        [ObservableProperty] private string _editName;
        [ObservableProperty] private string _editAddress;
        [ObservableProperty] private string _editNotes;
        [ObservableProperty] private string _editNameError;
        [ObservableProperty] private string _editAddressError;

        public bool IsFormValid =>
            string.IsNullOrEmpty(EditNameError) &&
            string.IsNullOrEmpty(EditAddressError) &&
            !string.IsNullOrWhiteSpace(EditName) &&
            !string.IsNullOrWhiteSpace(EditAddress);

        public Action<Contact> OnContactSelected { get; set; }
        public Action OnExit { get; set; }

        public ContactListViewModel(
            IContactService contactService,
            IComponentLocalizer<ContactListViewModel> localizer,
            NethereumWalletHostProvider walletHostProvider)
        {
            _contactService = contactService ?? throw new ArgumentNullException(nameof(contactService));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _walletHostProvider = walletHostProvider ?? throw new ArgumentNullException(nameof(walletHostProvider));

            _contactService.ContactChanged += OnContactChanged;
        }

        [RelayCommand]
        public async Task InitializeAsync()
        {
            IsLoading = true;
            ErrorMessage = null;

            try
            {
                LoadUserAccounts();
                await LoadContactsAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void LoadUserAccounts()
        {
            UserAccounts.Clear();
            var accounts = _walletHostProvider.GetAccounts();
            var searchLower = SearchText?.ToLowerInvariant();

            foreach (var account in accounts)
            {
                var nameMatch = string.IsNullOrWhiteSpace(searchLower) ||
                    (account.Name?.ToLowerInvariant().Contains(searchLower) ?? false);
                var addressMatch = string.IsNullOrWhiteSpace(searchLower) ||
                    account.Address.ToLowerInvariant().Contains(searchLower);

                if (nameMatch || addressMatch)
                {
                    UserAccounts.Add(new ContactItemViewModel
                    {
                        Address = account.Address,
                        Name = account.Name ?? "Account",
                        Notes = null,
                        CreatedAt = DateTime.MinValue,
                        IsUserAccount = true
                    });
                }
            }
        }

        private async Task LoadContactsAsync()
        {
            var contacts = string.IsNullOrWhiteSpace(SearchText)
                ? await _contactService.GetAllContactsAsync()
                : await _contactService.SearchAsync(SearchText);

            Contacts.Clear();
            foreach (var contact in contacts.OrderBy(c => c.Name))
            {
                Contacts.Add(new ContactItemViewModel
                {
                    Address = contact.Address,
                    Name = contact.Name,
                    Notes = contact.Notes,
                    CreatedAt = contact.CreatedAt
                });
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            _ = SearchAsync();
        }

        [RelayCommand]
        public async Task SearchAsync()
        {
            try
            {
                LoadUserAccounts();
                await LoadContactsAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        [RelayCommand]
        public void ShowAdd()
        {
            IsEditMode = false;
            SelectedContact = null;
            EditName = "";
            EditAddress = "";
            EditNotes = "";
            EditNameError = null;
            EditAddressError = null;
            IsShowingAddEdit = true;
        }

        [RelayCommand]
        public void ShowEdit(ContactItemViewModel contact)
        {
            if (contact == null) return;

            IsEditMode = true;
            SelectedContact = contact;
            EditName = contact.Name;
            EditAddress = contact.Address;
            EditNotes = contact.Notes;
            EditNameError = null;
            EditAddressError = null;
            IsShowingAddEdit = true;
        }

        [RelayCommand]
        public void CancelAddEdit()
        {
            IsShowingAddEdit = false;
            SelectedContact = null;
            EditName = "";
            EditAddress = "";
            EditNotes = "";
            EditNameError = null;
            EditAddressError = null;
        }

        [RelayCommand]
        public async Task SaveContactAsync()
        {
            ValidateName();
            ValidateAddress();

            if (!IsFormValid) return;

            IsLoading = true;
            ErrorMessage = null;

            try
            {
                var contact = new Contact
                {
                    Name = EditName.Trim(),
                    Address = EditAddress.Trim(),
                    Notes = string.IsNullOrWhiteSpace(EditNotes) ? null : EditNotes.Trim()
                };

                if (IsEditMode && SelectedContact != null)
                {
                    await _contactService.UpdateContactAsync(contact);
                }
                else
                {
                    var exists = await _contactService.ExistsAsync(contact.Address);
                    if (exists)
                    {
                        EditAddressError = _localizer.GetString(ContactListLocalizer.Keys.AddressAlreadyExists);
                        return;
                    }
                    await _contactService.AddContactAsync(contact);
                }

                IsShowingAddEdit = false;
                await LoadContactsAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task DeleteContactAsync(ContactItemViewModel contact)
        {
            if (contact == null) return;

            IsLoading = true;
            ErrorMessage = null;

            try
            {
                await _contactService.DeleteContactAsync(contact.Address);
                await LoadContactsAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public void SelectContact(ContactItemViewModel contact)
        {
            if (contact == null) return;

            var fullContact = new Contact
            {
                Address = contact.Address,
                Name = contact.Name,
                Notes = contact.Notes,
                CreatedAt = contact.CreatedAt
            };

            OnContactSelected?.Invoke(fullContact);
        }

        private void ValidateName()
        {
            if (string.IsNullOrWhiteSpace(EditName))
            {
                EditNameError = _localizer.GetString(ContactListLocalizer.Keys.NameRequired);
            }
            else
            {
                EditNameError = null;
            }
        }

        private void ValidateAddress()
        {
            if (string.IsNullOrWhiteSpace(EditAddress))
            {
                EditAddressError = _localizer.GetString(ContactListLocalizer.Keys.AddressRequired);
            }
            else if (!IsValidEthereumAddress(EditAddress))
            {
                EditAddressError = _localizer.GetString(ContactListLocalizer.Keys.InvalidAddress);
            }
            else
            {
                EditAddressError = null;
            }
        }

        private static bool IsValidEthereumAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return false;
            address = address.Trim();
            if (!address.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return false;
            if (address.Length != 42) return false;
            return address.Substring(2).All(c =>
                (c >= '0' && c <= '9') ||
                (c >= 'a' && c <= 'f') ||
                (c >= 'A' && c <= 'F'));
        }

        partial void OnEditNameChanged(string value) => ValidateName();
        partial void OnEditAddressChanged(string value) => ValidateAddress();

        private void OnContactChanged(object sender, ContactChangedEventArgs e)
        {
            _ = LoadContactsAsync();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _contactService.ContactChanged -= OnContactChanged;
        }
    }

    public class ContactItemViewModel : ObservableObject
    {
        public string Address { get; set; }
        public string Name { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsUserAccount { get; set; }

        public string FormattedAddress =>
            !string.IsNullOrEmpty(Address) && Address.Length > 10
                ? $"{Address.Substring(0, 6)}...{Address.Substring(Address.Length - 4)}"
                : Address;
    }
}
