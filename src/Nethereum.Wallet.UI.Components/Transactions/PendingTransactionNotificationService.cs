using System;
using System.Linq;
using Nethereum.Wallet.Services.Transactions;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.UI.Components.Abstractions;
using Nethereum.Wallet.UI.Components.Core.Localization;
using static Nethereum.Wallet.UI.Components.Transactions.PendingTransactionNotificationLocalizer;

namespace Nethereum.Wallet.UI.Components.Transactions
{
    public class PendingTransactionNotificationService : IDisposable
    {
        private readonly IPendingTransactionService _pendingTransactionService;
        private readonly IWalletNotificationService _notificationService;
        private readonly IComponentLocalizer<PendingTransactionNotificationService> _localizer;
        private readonly IChainManagementService _chainManagementService;
        private bool _isActive = true;
        
        public Action<string>? OnNavigateToExplorer { get; set; }
        public Action<TransactionInfo>? OnShowTransactionDetails { get; set; }
        
        public PendingTransactionNotificationService(
            IPendingTransactionService pendingTransactionService,
            IWalletNotificationService notificationService,
            IComponentLocalizer<PendingTransactionNotificationService> localizer,
            IChainManagementService chainManagementService)
        {
            _pendingTransactionService = pendingTransactionService;
            _notificationService = notificationService;
            _localizer = localizer;
            _chainManagementService = chainManagementService;
            
            _pendingTransactionService.TransactionSubmitted += OnTransactionSubmitted;
            _pendingTransactionService.TransactionStatusChanged += OnTransactionStatusChanged;
            _pendingTransactionService.TransactionConfirmed += OnTransactionConfirmed;
            _pendingTransactionService.TransactionFailed += OnTransactionFailed;
        }
        public bool IsActive
        {
            get => _isActive;
            set => _isActive = value;
        }
        
        private void OnTransactionSubmitted(object? sender, TransactionSubmittedEventArgs e)
        {
            if (!_isActive) return;
            
            var displayName = e.Transaction.DisplayName ?? _localizer.GetString(Keys.UnknownTransaction);
            var shortHash = GetShortHash(e.Transaction.Hash);
            
            var message = _localizer.GetString(Keys.TransactionSubmittedWithHash, 
                displayName, shortHash);
                
            var action = new NotificationAction
            {
                Label = _localizer.GetString(Keys.ViewDetails),
                OnClick = () => NavigateToExplorer(e.Transaction)
            };
            
            _notificationService.ShowInfoWithAction(message, action);
        }
        
        private void OnTransactionStatusChanged(object? sender, TransactionStatusChangedEventArgs e)
        {
            if (!_isActive) return;
            
            // Only show notifications for specific status changes
            switch (e.NewStatus)
            {
                case TransactionStatus.Mining:
                    var displayName = e.Transaction.DisplayName ?? _localizer.GetString(Keys.UnknownTransaction);
                    var shortHash = GetShortHash(e.Transaction.Hash);
                    var miningMessage = _localizer.GetString(Keys.TransactionMiningWithHash, 
                        displayName, shortHash);
                        
                    var action = new NotificationAction
                    {
                        Label = _localizer.GetString(Keys.ViewOnExplorer),
                        OnClick = () => NavigateToExplorer(e.Transaction)
                    };
                    
                    _notificationService.ShowInfoWithAction(miningMessage, action);
                    break;
                    
                case TransactionStatus.Pending when e.OldStatus == TransactionStatus.Mining:
                    // Transaction went back to pending from mining (reorg?)
                    var reorgMessage = _localizer.GetString(Keys.TransactionReorgWithHash, 
                        e.Transaction.DisplayName ?? _localizer.GetString(Keys.UnknownTransaction),
                        GetShortHash(e.Transaction.Hash));
                    _notificationService.ShowWarning(reorgMessage);
                    break;
            }
        }
        
        private void OnTransactionConfirmed(object? sender, TransactionConfirmedEventArgs e)
        {
            if (!_isActive) return;
            
            var displayName = e.Transaction.DisplayName ?? _localizer.GetString(Keys.UnknownTransaction);
            var shortHash = GetShortHash(e.Transaction.Hash);
            
            var message = _localizer.GetString(Keys.TransactionConfirmedWithHash, 
                displayName, shortHash);
            
            if (e.Transaction.Confirmations > 0)
            {
                message += " " + _localizer.GetString(Keys.ConfirmationCount, e.Transaction.Confirmations);
            }
            
            var action = new NotificationAction
            {
                Label = _localizer.GetString(Keys.ViewOnExplorer),
                OnClick = () => NavigateToExplorer(e.Transaction)
            };
            
            _notificationService.ShowSuccessWithAction(message, action);
        }
        
        private void OnTransactionFailed(object? sender, TransactionFailedEventArgs e)
        {
            if (!_isActive) return;
            
            var displayName = e.Transaction.DisplayName ?? _localizer.GetString(Keys.UnknownTransaction);
            var shortHash = GetShortHash(e.Transaction.Hash);
            
            var message = _localizer.GetString(Keys.TransactionFailedWithHash, 
                displayName, shortHash);
            
            if (!string.IsNullOrEmpty(e.Reason))
            {
                message += ": " + e.Reason;
            }
            else
            {
                message += ". " + _localizer.GetString(Keys.TransactionFailedGeneric);
            }
            
            _notificationService.ShowError(message);
        }
        
        private async void NavigateToExplorer(TransactionInfo transaction)
        {
            try
            {
                var chain = await _chainManagementService.GetChainAsync(transaction.ChainId);
                if (chain?.Explorers?.Count > 0)
                {
                    var explorerUrl = chain.Explorers.First();
                    var fullUrl = $"{explorerUrl.TrimEnd('/')}/tx/{transaction.Hash}";
                    OnNavigateToExplorer?.Invoke(fullUrl);
                }
                else
                {
                    OnShowTransactionDetails?.Invoke(transaction);
                }
            }
            catch
            {
                OnShowTransactionDetails?.Invoke(transaction);
            }
        }
        
        private string GetShortHash(string hash)
        {
            if (string.IsNullOrEmpty(hash) || hash.Length < 10)
                return hash;
            
            return $"{hash.Substring(0, 6)}...{hash.Substring(hash.Length - 4)}";
        }
        
        public void Dispose()
        {
            _pendingTransactionService.TransactionSubmitted -= OnTransactionSubmitted;
            _pendingTransactionService.TransactionStatusChanged -= OnTransactionStatusChanged;
            _pendingTransactionService.TransactionConfirmed -= OnTransactionConfirmed;
            _pendingTransactionService.TransactionFailed -= OnTransactionFailed;
        }
    }
}