using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Services.Tokens;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.UI.Components.Core.Localization;

namespace Nethereum.Wallet.UI.Components.Holdings
{
    public enum ScanStatusIndicator
    {
        NotScanned,
        PartiallyScanned,
        FullyScanned,
        NeedsRescan
    }

    public partial class EditHoldingsViewModel : ObservableObject
    {
        private readonly IChainManagementService _chainService;
        private readonly IWalletVaultService _walletVaultService;
        private readonly ITokenManagementService _tokenManagementService;
        private readonly IComponentLocalizer<EditHoldingsViewModel> _localizer;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _errorMessage;

        [ObservableProperty] private ObservableCollection<SelectableAccountViewModel> _accounts = new();
        [ObservableProperty] private ObservableCollection<SelectableNetworkViewModel> _networks = new();

        [ObservableProperty] private Dictionary<string, Dictionary<long, bool>> _accountChainScanStatus = new();
        [ObservableProperty] private int _accountsNeedingScan;
        [ObservableProperty] private int _networksNeedingScan;
        [ObservableProperty] private int _totalScansRequired;

        private HoldingsSettings _originalSettings;
        private List<long> _allChainIds = new();

        public int SelectedAccountCount => Accounts.Count(a => a.IsSelected);
        public int SelectedNetworkCount => Networks.Count(n => n.IsSelected);
        public string SelectionSummary => $"{SelectedAccountCount} accounts × {SelectedNetworkCount} networks selected";

        public string ScanSummary => TotalScansRequired > 0
            ? _localizer.GetString(EditHoldingsLocalizer.Keys.ScanSummaryFormat)
                .Replace("{0}", AccountsNeedingScan.ToString())
                .Replace("{1}", NetworksNeedingScan.ToString())
                .Replace("{2}", TotalScansRequired.ToString())
            : _localizer.GetString(EditHoldingsLocalizer.Keys.AllScanned);

        public bool HasChanges
        {
            get
            {
                if (_originalSettings == null) return false;

                var currentAccounts = Accounts.Where(a => a.IsSelected).Select(a => a.Address).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var originalAccounts = _originalSettings.SelectedAccountAddresses?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

                var currentNetworks = Networks.Where(n => n.IsSelected).Select(n => n.ChainId).ToHashSet();
                var originalNetworks = _originalSettings.SelectedChainIds?.ToHashSet() ?? new HashSet<long>();

                return !currentAccounts.SetEquals(originalAccounts) || !currentNetworks.SetEquals(originalNetworks);
            }
        }

        public Func<HoldingsSettings, Task> OnSave { get; set; }
        public Func<Task> OnCancel { get; set; }

        public EditHoldingsViewModel(
            IChainManagementService chainService,
            IWalletVaultService walletVaultService,
            ITokenManagementService tokenManagementService,
            IComponentLocalizer<EditHoldingsViewModel> localizer)
        {
            _chainService = chainService ?? throw new ArgumentNullException(nameof(chainService));
            _walletVaultService = walletVaultService ?? throw new ArgumentNullException(nameof(walletVaultService));
            _tokenManagementService = tokenManagementService ?? throw new ArgumentNullException(nameof(tokenManagementService));
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
        }

        [RelayCommand]
        public async Task InitializeAsync(HoldingsSettings currentSettings)
        {
            IsLoading = true;
            ErrorMessage = null;
            _originalSettings = currentSettings ?? new HoldingsSettings();

            try
            {
                var walletAccounts = await _walletVaultService.GetAccountsAsync();
                var chains = await _chainService.GetAllChainsAsync();

                _allChainIds = chains.Where(c => !c.IsTestnet).Select(c => (long)c.ChainId).ToList();

                Accounts.Clear();
                foreach (var account in walletAccounts)
                {
                    var viewModel = new SelectableAccountViewModel(account)
                    {
                        IsSelected = _originalSettings.SelectedAccountAddresses?.Contains(account.Address, StringComparer.OrdinalIgnoreCase) ?? false
                    };
                    viewModel.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(SelectableAccountViewModel.IsSelected))
                        {
                            OnPropertyChanged(nameof(SelectedAccountCount));
                            OnPropertyChanged(nameof(SelectionSummary));
                            OnPropertyChanged(nameof(HasChanges));
                            UpdateScanSummary();
                        }
                    };
                    Accounts.Add(viewModel);
                }

                Networks.Clear();
                foreach (var chain in chains.Where(c => !c.IsTestnet))
                {
                    var viewModel = new SelectableNetworkViewModel(chain)
                    {
                        IsSelected = _originalSettings.SelectedChainIds?.Contains((long)chain.ChainId) ?? false
                    };
                    viewModel.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(SelectableNetworkViewModel.IsSelected))
                        {
                            OnPropertyChanged(nameof(SelectedNetworkCount));
                            OnPropertyChanged(nameof(SelectionSummary));
                            OnPropertyChanged(nameof(HasChanges));
                            UpdateScanSummary();
                        }
                    };
                    Networks.Add(viewModel);
                }

                await LoadScanStatusesAsync();

                OnPropertyChanged(nameof(SelectedAccountCount));
                OnPropertyChanged(nameof(SelectedNetworkCount));
                OnPropertyChanged(nameof(SelectionSummary));
                OnPropertyChanged(nameof(ScanSummary));
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

        private async Task LoadScanStatusesAsync()
        {
            AccountChainScanStatus.Clear();

            foreach (var account in Accounts)
            {
                var chainStatuses = new Dictionary<long, bool>();

                foreach (var chainId in _allChainIds)
                {
                    var isComplete = await _tokenManagementService.IsDiscoveryCompleteAsync(account.Address, chainId);
                    chainStatuses[chainId] = isComplete;
                }

                AccountChainScanStatus[account.Address] = chainStatuses;
            }

            UpdateScanSummary();
        }

        private void UpdateScanSummary()
        {
            var selectedAccounts = Accounts.Where(a => a.IsSelected).Select(a => a.Address).ToList();
            var selectedNetworks = Networks.Where(n => n.IsSelected).Select(n => n.ChainId).ToList();

            if (!selectedAccounts.Any() || !selectedNetworks.Any())
            {
                AccountsNeedingScan = 0;
                NetworksNeedingScan = 0;
                TotalScansRequired = 0;
                OnPropertyChanged(nameof(ScanSummary));
                return;
            }

            var accountsNeedingScan = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var networksNeedingScan = new HashSet<long>();
            var totalScans = 0;

            foreach (var account in selectedAccounts)
            {
                if (!AccountChainScanStatus.TryGetValue(account, out var chainStatuses))
                    continue;

                foreach (var chainId in selectedNetworks)
                {
                    if (!chainStatuses.TryGetValue(chainId, out var isComplete) || !isComplete)
                    {
                        accountsNeedingScan.Add(account);
                        networksNeedingScan.Add(chainId);
                        totalScans++;
                    }
                }
            }

            AccountsNeedingScan = accountsNeedingScan.Count;
            NetworksNeedingScan = networksNeedingScan.Count;
            TotalScansRequired = totalScans;
            OnPropertyChanged(nameof(ScanSummary));
        }

        public ScanStatusIndicator GetAccountScanStatus(string address)
        {
            if (!AccountChainScanStatus.TryGetValue(address, out var chainStatuses))
                return ScanStatusIndicator.NotScanned;

            var selectedNetworks = Networks.Where(n => n.IsSelected).Select(n => n.ChainId).ToList();
            if (!selectedNetworks.Any())
                return ScanStatusIndicator.NotScanned;

            var scannedCount = selectedNetworks.Count(c => chainStatuses.TryGetValue(c, out var isComplete) && isComplete);

            if (scannedCount == 0)
                return ScanStatusIndicator.NotScanned;
            if (scannedCount == selectedNetworks.Count)
                return ScanStatusIndicator.FullyScanned;
            return ScanStatusIndicator.PartiallyScanned;
        }

        public int GetAccountScannedChainCount(string address)
        {
            if (!AccountChainScanStatus.TryGetValue(address, out var chainStatuses))
                return 0;

            return chainStatuses.Count(c => c.Value);
        }

        [RelayCommand]
        public void ToggleAccount(SelectableAccountViewModel account)
        {
            if (account != null)
            {
                account.IsSelected = !account.IsSelected;
            }
        }

        [RelayCommand]
        public void ToggleNetwork(SelectableNetworkViewModel network)
        {
            if (network != null)
            {
                network.IsSelected = !network.IsSelected;
            }
        }

        [RelayCommand]
        public void SelectAllAccounts()
        {
            foreach (var account in Accounts)
            {
                account.IsSelected = true;
            }
        }

        [RelayCommand]
        public void DeselectAllAccounts()
        {
            foreach (var account in Accounts)
            {
                account.IsSelected = false;
            }
        }

        [RelayCommand]
        public void SelectAllNetworks()
        {
            foreach (var network in Networks)
            {
                network.IsSelected = true;
            }
        }

        [RelayCommand]
        public void DeselectAllNetworks()
        {
            foreach (var network in Networks)
            {
                network.IsSelected = false;
            }
        }

        [RelayCommand]
        public async Task SaveAsync()
        {
            var newSettings = new HoldingsSettings
            {
                SelectedAccountAddresses = Accounts
                    .Where(a => a.IsSelected)
                    .Select(a => a.Address)
                    .ToList(),
                SelectedChainIds = Networks
                    .Where(n => n.IsSelected)
                    .Select(n => n.ChainId)
                    .ToList(),
                ForceRescanAccountAddresses = Accounts
                    .Where(a => a.IsSelected && a.ForceRescan)
                    .Select(a => a.Address)
                    .ToList(),
                LastScanned = _originalSettings?.LastScanned
            };

            if (OnSave != null)
                await OnSave(newSettings);
        }

        [RelayCommand]
        public async Task CancelAsync()
        {
            if (OnCancel != null)
                await OnCancel();
        }

        [RelayCommand]
        public void Reset()
        {
            Accounts.Clear();
            Networks.Clear();
            _originalSettings = null;
        }
    }
}
