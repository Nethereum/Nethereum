using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Services.Transactions;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.UI.Components.Abstractions;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Services;

namespace Nethereum.Wallet.UI.Components.Transactions
{
    public partial class TransactionHistoryViewModel : ObservableObject, IDisposable
    {
        private readonly IWalletStorageService _storageService;
        private readonly IPendingTransactionService _transactionService;
        private readonly IComponentLocalizer<TransactionHistoryViewModel> _localizer;
        private readonly IWalletDialogService _dialogService;
        private readonly NethereumWalletHostProvider _walletHostProvider;
        private readonly IChainManagementService? _chainManagementService;
        
        [ObservableProperty] private ObservableCollection<TransactionInfo> _pendingTransactions = new();
        [ObservableProperty] private ObservableCollection<TransactionInfo> _recentTransactions = new();
        [ObservableProperty] private TransactionInfo? _selectedTransaction;
        [ObservableProperty] private bool _showDetails;
        
        [ObservableProperty] private string _filterText = "";
        [ObservableProperty] private string? _filterTextError;
        
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string? _errorMessage;
        
        public event EventHandler<string>? CopyToClipboardRequested;
        public event EventHandler<string>? OpenUrlRequested;
        
        public TransactionHistoryViewModel(
            IWalletStorageService storageService,
            IPendingTransactionService transactionService,
            IComponentLocalizer<TransactionHistoryViewModel> localizer,
            IWalletDialogService dialogService,
            NethereumWalletHostProvider walletHostProvider,
            IChainManagementService? chainManagementService = null)
        {
            _storageService = storageService;
            _transactionService = transactionService;
            _localizer = localizer;
            _dialogService = dialogService;
            _walletHostProvider = walletHostProvider;
            _chainManagementService = chainManagementService;
            
            _transactionService.TransactionStatusChanged += OnTransactionStatusChanged;
            _transactionService.TransactionConfirmed += OnTransactionConfirmed;
            _transactionService.TransactionFailed += OnTransactionFailed;
        }
        
        public async Task InitializeAsync()
        {
            IsLoading = true;
            ErrorMessage = null;
            
            try
            {
                var chainId = new BigInteger(_walletHostProvider.SelectedNetworkChainId);
                
                var pending = await _storageService.GetPendingTransactionsAsync(chainId);
                var recent = await _storageService.GetRecentTransactionsAsync(chainId);
                
                PendingTransactions = new ObservableCollection<TransactionInfo>(pending);
                RecentTransactions = new ObservableCollection<TransactionInfo>(recent);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"{_localizer.GetString(TransactionHistoryLocalizer.Keys.LoadingTransactions)}: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        partial void OnFilterTextChanged(string value)
        {
            ValidateFilterText();
            OnPropertyChanged(nameof(FilteredPendingTransactions));
            OnPropertyChanged(nameof(FilteredRecentTransactions));
        }
        
        private void ValidateFilterText()
        {
            if (FilterText.Length > 100)
            {
                FilterTextError = _localizer.GetString(TransactionHistoryLocalizer.Keys.FilterPlaceholder);
            }
            else if (!string.IsNullOrWhiteSpace(FilterText) && FilterText.Length < 3)
            {
                FilterTextError = _localizer.GetString(TransactionHistoryLocalizer.Keys.FilterPlaceholder);
            }
            else
            {
                FilterTextError = null;
            }
        }
        
        public bool IsFormValid => string.IsNullOrEmpty(FilterTextError);
        
        [RelayCommand]
        private async Task RetryTransactionAsync(TransactionInfo transaction)
        {
            if (transaction.Status != TransactionStatus.Failed && 
                transaction.Status != TransactionStatus.Dropped) 
                return;
            
            var confirm = await _dialogService.ShowConfirmationAsync(
                _localizer.GetString(TransactionHistoryLocalizer.Keys.RetryConfirmTitle),
                _localizer.GetString(TransactionHistoryLocalizer.Keys.RetryConfirmMessage));
            
            if (!confirm) return;
            
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                
                var newHash = await _transactionService.RetryTransactionAsync(transaction);
                
                await InitializeAsync();
                
                await _dialogService.ShowSuccessAsync(
                    _localizer.GetString(TransactionHistoryLocalizer.Keys.RetrySuccess),
                    _localizer.GetString(TransactionHistoryLocalizer.Keys.RetrySubmitted));
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                await _dialogService.ShowErrorAsync(
                    _localizer.GetString(TransactionHistoryLocalizer.Keys.RetryFailed),
                    ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        [RelayCommand]
        private void RequestCopyHash(TransactionInfo transaction)
        {
            CopyToClipboardRequested?.Invoke(this, transaction.Hash);
        }
        
        [RelayCommand]
        private async Task RequestViewOnExplorerAsync(TransactionInfo transaction)
        {
            var url = await GetExplorerUrlAsync(transaction);
            if (!string.IsNullOrEmpty(url))
            {
                OpenUrlRequested?.Invoke(this, url);
            }
        }
        
        [RelayCommand]
        private void ShowTransactionDetails(TransactionInfo transaction)
        {
            SelectedTransaction = transaction;
            ShowDetails = true;
        }
        
        [RelayCommand]
        private void CloseDetails()
        {
            ShowDetails = false;
            SelectedTransaction = null;
        }
        
        [RelayCommand]
        private void ClearFilter()
        {
            FilterText = "";
        }
        
        public IEnumerable<TransactionInfo> FilteredPendingTransactions =>
            string.IsNullOrWhiteSpace(FilterText) || !string.IsNullOrEmpty(FilterTextError)
                ? PendingTransactions 
                : PendingTransactions.Where(t => 
                    t.DisplayName.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                    t.Hash.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
        
        public IEnumerable<TransactionInfo> FilteredRecentTransactions =>
            string.IsNullOrWhiteSpace(FilterText) || !string.IsNullOrEmpty(FilterTextError)
                ? RecentTransactions
                : RecentTransactions.Where(t =>
                    t.DisplayName.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                    t.Hash.Contains(FilterText, StringComparison.OrdinalIgnoreCase));
        
        public int PendingCount => PendingTransactions.Count;
        public int RecentCount => RecentTransactions.Count;
        
        public bool HasPendingTransactions => PendingTransactions.Any();
        public bool HasRecentTransactions => RecentTransactions.Any();
        
        private async Task<string?> GetExplorerUrlAsync(TransactionInfo transaction)
        {
            if (string.IsNullOrEmpty(transaction.Hash)) return null;
            
            try
            {
                if (_chainManagementService != null)
                {
                    var chain = await _chainManagementService.GetChainAsync(transaction.ChainId);
                    
                    if (chain?.Explorers?.Count > 0)
                    {
                        var explorerUrl = chain.Explorers.First();
                        return $"{explorerUrl.TrimEnd('/')}/tx/{transaction.Hash}";
                    }
                }
            }
            catch
            {
            }
            
            return null;
        }
        
        private void OnTransactionStatusChanged(object? sender, TransactionStatusChangedEventArgs e)
        {
            _ = InitializeAsync();
        }
        
        private async void OnTransactionConfirmed(object? sender, TransactionConfirmedEventArgs e)
        {
            await _dialogService.ShowSuccessAsync(
                _localizer.GetString(TransactionHistoryLocalizer.Keys.TransactionConfirmed),
                $"{e.Transaction.DisplayName} {_localizer.GetString(TransactionHistoryLocalizer.Keys.HasBeenConfirmed)}");
        }
        
        private async void OnTransactionFailed(object? sender, TransactionFailedEventArgs e)
        {
            await _dialogService.ShowErrorAsync(
                _localizer.GetString(TransactionHistoryLocalizer.Keys.TransactionFailed),
                $"{e.Transaction.DisplayName} {_localizer.GetString(TransactionHistoryLocalizer.Keys.HasFailed)}");
        }
        
        public void Dispose()
        {
            _transactionService.TransactionStatusChanged -= OnTransactionStatusChanged;
            _transactionService.TransactionConfirmed -= OnTransactionConfirmed;
            _transactionService.TransactionFailed -= OnTransactionFailed;
        }
    }
}