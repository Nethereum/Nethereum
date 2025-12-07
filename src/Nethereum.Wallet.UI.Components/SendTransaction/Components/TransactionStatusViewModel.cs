using System;
using System.Numerics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.Services.Transactions;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Hosting;
using Nethereum.Web3;
using Nethereum.Util;

namespace Nethereum.Wallet.UI.Components.SendTransaction.Components
{
    public partial class TransactionStatusViewModel : ObservableObject, IDisposable
    {
        private readonly IComponentLocalizer<TransactionStatusViewModel> _localizer;
        private readonly IPendingTransactionService? _pendingTransactionService;
        private readonly IChainManagementService _chainManagementService;
        private readonly NethereumWalletHostProvider _walletHostProvider;
        
        [ObservableProperty] private string? _transactionHash;
        [ObservableProperty] private bool _isSuccess = false;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private string? _successMessage;
        [ObservableProperty] private string _networkName = "";
        [ObservableProperty] private string? _explorerUrl;
        
        [ObservableProperty] private Nethereum.Wallet.Services.Transactions.TransactionStatus _currentStatus = Nethereum.Wallet.Services.Transactions.TransactionStatus.Pending;
        [ObservableProperty] private int _confirmationCount = 0;
        [ObservableProperty] private string? _gasUsed;
        [ObservableProperty] private string? _actualCost;
        [ObservableProperty] private bool _isMonitoring = false;
        [ObservableProperty] private string? _from;
        [ObservableProperty] private string? _to;
        [ObservableProperty] private string? _value;
        [ObservableProperty] private DateTime _submittedAt;
        [ObservableProperty] private string _currencySymbol = "ETH";
        
        public Action? OnClose { get; set; }
        public Action? OnViewTransaction { get; set; }
        public Action? OnNavigateToHistory { get; set; }
        public Action? OnNewTransaction { get; set; }
        public int AutoCloseConfirmationCount { get; set; } = 12;
        
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        public bool CanViewTransaction => !string.IsNullOrEmpty(TransactionHash);
        public bool HasExplorerUrl => !string.IsNullOrEmpty(ExplorerUrl);
        
        private System.Threading.Timer? _monitoringTimer;
        private IWeb3? _web3;
        
        public TransactionStatusViewModel(
            IComponentLocalizer<TransactionStatusViewModel> localizer,
            IPendingTransactionService? pendingTransactionService,
            IChainManagementService chainManagementService,
            NethereumWalletHostProvider walletHostProvider)
        {
            _localizer = localizer;
            _pendingTransactionService = pendingTransactionService;
            _chainManagementService = chainManagementService;
            _walletHostProvider = walletHostProvider;
        }
        
        public async Task InitializeAsync(
            string transactionHash,
            string from,
            string to,
            string value,
            bool startMonitoring = true)
        {
            TransactionHash = transactionHash;
            From = from;
            To = to;
            Value = value;
            SubmittedAt = DateTime.Now;
            IsSuccess = true;
            SuccessMessage = _localizer.GetString(TransactionStatusLocalizer.Keys.TransactionSubmitted);
            
            var chainId = _walletHostProvider.SelectedNetworkChainId;
            var chain = await _chainManagementService.GetChainAsync(new BigInteger(chainId));
            NetworkName = chain?.ChainName ?? $"Chain {chainId}";
            CurrencySymbol = chain?.NativeCurrency?.Symbol ?? "ETH";
            
            if (chain?.Explorers?.Count > 0)
            {
                var explorerUrl = chain.Explorers[0];
                ExplorerUrl = $"{explorerUrl}/tx/{TransactionHash}";
            }
            
            _web3 = await _walletHostProvider.GetWeb3Async();
            
            if (startMonitoring)
            {
                StartMonitoring();
            }
        }
        
        public void InitializeWithError(string errorMessage)
        {
            IsSuccess = false;
            ErrorMessage = errorMessage;
            CurrentStatus = Nethereum.Wallet.Services.Transactions.TransactionStatus.Failed;
        }
        
        [RelayCommand]
        private void StartMonitoring()
        {
            if (string.IsNullOrEmpty(TransactionHash)) return;
            
            IsMonitoring = true;
            
            _monitoringTimer = new System.Threading.Timer(async _ =>
            {
                await MonitorTransactionStatusAsync();
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(3));
        }
        
        [RelayCommand]
        private async Task MonitorTransactionStatusAsync()
        {
            if (_web3 == null || string.IsNullOrEmpty(TransactionHash)) return;
            
            try
            {
                var receipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(TransactionHash);
                
                if (receipt != null)
                {
                   
                    if (receipt.Status?.Value == 1)
                    {
                        
                        CurrentStatus = Nethereum.Wallet.Services.Transactions.TransactionStatus.Confirmed;
                        SuccessMessage = _localizer.GetString(TransactionStatusLocalizer.Keys.TransactionConfirmed);
                        
                        var currentBlock = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
                        ConfirmationCount = (int)(currentBlock.Value - receipt.BlockNumber.Value) + 1;
                       
                        if (receipt.GasUsed != null)
                        {
                            GasUsed = receipt.GasUsed.Value.ToString();
                            
                            var tx = await _web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(TransactionHash);
                            if (tx?.GasPrice != null)
                            {
                                var costWei = receipt.GasUsed.Value * tx.GasPrice.Value;
                                var costEther = UnitConversion.Convert.FromWei(costWei);
                                ActualCost = $"{costEther:F6} {CurrencySymbol}";
                            }
                        }
                        
                        OnPropertyChanged(nameof(CurrentStatus));
                        OnPropertyChanged(nameof(SuccessMessage));
                        OnPropertyChanged(nameof(ConfirmationCount));
                        OnPropertyChanged(nameof(GasUsed));
                        OnPropertyChanged(nameof(ActualCost));
                        
                        if (AutoCloseConfirmationCount > 0 && ConfirmationCount >= AutoCloseConfirmationCount)
                        {
                            StopMonitoring();
                            OnClose?.Invoke();
                        }
                    }
                    else if (receipt.Status?.Value == 0)
                    {
                        CurrentStatus = Nethereum.Wallet.Services.Transactions.TransactionStatus.Failed;
                        ErrorMessage = _localizer.GetString(TransactionStatusLocalizer.Keys.TransactionFailedMessage);
                        
                        OnPropertyChanged(nameof(CurrentStatus));
                        OnPropertyChanged(nameof(ErrorMessage));
                        
                        StopMonitoring();
                    }
                }
                else
                {
                    var previousStatus = CurrentStatus;
                    CurrentStatus = Nethereum.Wallet.Services.Transactions.TransactionStatus.Pending;
                    
                    var tx = await _web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(TransactionHash);
                    if (tx != null && tx.BlockNumber != null)
                    {
                        CurrentStatus = Nethereum.Wallet.Services.Transactions.TransactionStatus.Mining;
                    }
                    
                    if (previousStatus != CurrentStatus)
                    {
                        OnPropertyChanged(nameof(CurrentStatus));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error monitoring transaction: {ex.Message}");
            }
        }
        
        public void StopMonitoring()
        {
            if (!IsMonitoring) return;
            
            IsMonitoring = false;
            _monitoringTimer?.Dispose();
            _monitoringTimer = null;
            
            OnPropertyChanged(nameof(IsMonitoring));
        }
        
        [RelayCommand]
        private Task CloseAsync()
        {
            OnClose?.Invoke();
            return Task.CompletedTask;
        }
        
        [RelayCommand]
        private Task ViewTransactionAsync()
        {
            OnViewTransaction?.Invoke();
            return Task.CompletedTask;
        }
        
        [RelayCommand]
        private Task NavigateToHistoryAsync()
        {
            OnNavigateToHistory?.Invoke();
            return Task.CompletedTask;
        }
        
        [RelayCommand]
        private Task NewTransactionAsync()
        {
            StopMonitoring();
            OnNewTransaction?.Invoke();
            return Task.CompletedTask;
        }
        
        public void Dispose()
        {
            StopMonitoring();
        }
    }
}
