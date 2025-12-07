using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.SendTransaction.Models;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Services.Transactions;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.Diagnostics;

namespace Nethereum.Wallet.UI.Components.SendTransaction
{
    public partial class SendNativeTokenViewModel : ObservableObject
    {
        private readonly IComponentLocalizer<SendNativeTokenViewModel> _localizer;
        private readonly IChainManagementService _chainManagementService;
        private readonly NethereumWalletHostProvider _walletHostProvider;
        private readonly IPendingTransactionService? _pendingTransactionService;
        
        [ObservableProperty] private TokenNativeTransferModel _nativeTransfer = null!;
        [ObservableProperty] private TransactionViewModel _transaction = null!;
        
        [ObservableProperty] private int _currentStep = 1;
        [ObservableProperty] private int _totalSteps = 3;
        
        [ObservableProperty] private bool _isLoading = false;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private string? _successMessage;
        [ObservableProperty] private string? _transactionHash;
        
        public SendNativeTokenViewModel(
            IComponentLocalizer<SendNativeTokenViewModel> localizer,
            TokenNativeTransferModel nativeTransferModel,
            TransactionViewModel transactionViewModel,
            IChainManagementService chainManagementService,
            NethereumWalletHostProvider walletHostProvider,
            IPendingTransactionService? pendingTransactionService = null)
        {
            _localizer = localizer;
            _nativeTransfer = nativeTransferModel;
            _transaction = transactionViewModel;
            _chainManagementService = chainManagementService;
            _walletHostProvider = walletHostProvider;
            _pendingTransactionService = pendingTransactionService;
            
            SetupModelSynchronization();
        }
        
        public async Task InitializeAsync()
        {
            try
            {
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", "InitializeAsync start");
                // Get selected account and network info (copied from working TokenTransferViewModel)
                var selectedAccount = _walletHostProvider.GetSelectedAccount();
                if (selectedAccount == null)
                {
                    ErrorMessage = "No account selected";
                    return;
                }
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"InitializeAsync selected account {selectedAccount.Address}");
                
                NativeTransfer.FromAddress = selectedAccount.Address;
                
                var chainId = _walletHostProvider.SelectedNetworkChainId;
                var chain = await _chainManagementService.GetChainAsync(new BigInteger(chainId));
                
                string tokenSymbol = "ETH";
                if (chain?.NativeCurrency?.Symbol != null)
                {
                    tokenSymbol = chain.NativeCurrency.Symbol;
                }
                
                NativeTransfer.TokenSymbol = tokenSymbol;
                NativeTransfer.TokenDecimals = 18;
                
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", "InitializeAsync requesting read-only Web3");
                var web3 = await _walletHostProvider.GetWeb3Async();
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", "InitializeAsync read-only Web3 ready");
                var balance = await web3.Eth.GetBalance.SendRequestAsync(selectedAccount.Address);
                NativeTransfer.AvailableBalance = balance.Value;
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"InitializeAsync balance loaded {balance.Value} wei");
                
                Transaction.Transaction.FromAddress = selectedAccount.Address;
                Transaction.Initialize(Transaction.Transaction);
            }
            catch (Exception ex)
            {
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"InitializeAsync error: {ex.Message}");
                ErrorMessage = $"Failed to initialize: {ex.Message}";
            }
        }
        
        public void Initialize(string fromAddress, string tokenSymbol = "ETH", int tokenDecimals = 18)
        {
            NativeTransfer.FromAddress = fromAddress;
            NativeTransfer.TokenSymbol = tokenSymbol;
            NativeTransfer.TokenDecimals = tokenDecimals;
            
            Transaction.Transaction.FromAddress = fromAddress;
            Transaction.Initialize(Transaction.Transaction);
        }
        
        private void SetupModelSynchronization()
        {
            NativeTransfer.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TokenNativeTransferModel.RecipientAddress))
                {
                    Transaction.Transaction.RecipientAddress = NativeTransfer.RecipientAddress;
                }
                else if (e.PropertyName == nameof(TokenNativeTransferModel.Amount))
                {
                    Transaction.Transaction.Amount = NativeTransfer.Amount;
                }
                else if (e.PropertyName == nameof(TokenNativeTransferModel.TransactionData))
                {
                    Transaction.Transaction.Data = NativeTransfer.TransactionData;
                }
                else if (e.PropertyName == nameof(TokenNativeTransferModel.Nonce))
                {
                    Transaction.Transaction.Nonce = NativeTransfer.Nonce;
                }
                
                OnPropertyChanged(nameof(CanProceedToNextStep));
                OnPropertyChanged(nameof(CanSendTransaction));
            };
        }
        
        public bool CanProceedToNextStep => CurrentStep switch
        {
            1 => IsStep1Valid,
            2 => IsStep2Valid,
            _ => false
        };
        
        public bool CanGoBack => CurrentStep > 1;
        
        public bool CanSendTransaction => 
            CurrentStep == 2 && 
            NativeTransfer.IsValid && 
            Transaction.HasValidTransaction();
        
        public bool IsStep1Valid => 
            !string.IsNullOrWhiteSpace(NativeTransfer.RecipientAddress) &&
            !string.IsNullOrWhiteSpace(NativeTransfer.Amount) &&
            NativeTransfer.ValidateAmountBalance() &&
            !NativeTransfer.HasFieldErrors(nameof(TokenNativeTransferModel.RecipientAddress)) &&
            !NativeTransfer.HasFieldErrors(nameof(TokenNativeTransferModel.Amount));
        
        public bool IsStep2Valid => 
            IsStep1Valid &&
            Transaction.Transaction.GasConfiguration?.IsValid == true &&
            !string.IsNullOrWhiteSpace(Transaction.Transaction.Nonce);
        
        [RelayCommand]
        private async Task NextStepAsync()
        {
            Console.WriteLine($"[DEBUG] NextStepAsync: CurrentStep={CurrentStep}, CanProceedToNextStep={CanProceedToNextStep}");
            
            if (!CanProceedToNextStep || CurrentStep >= TotalSteps) return;
            
            Transaction.ValidationError = null;
            
            if (CurrentStep == 1)
            {
                NativeTransfer.ValidateAll();
                if (!IsStep1Valid)
                {
                    ErrorMessage = _localizer.GetString(SendNativeTokenLocalizer.Keys.PleaseCorrectErrors);
                    return;
                }
            }
            
            CurrentStep++;
            Console.WriteLine($"[DEBUG] Moved to step {CurrentStep}");
            
            if (CurrentStep == 2)
            {
                Console.WriteLine("[DEBUG] Loading gas configuration and nonce...");
                await Transaction.EstimateGasLimitAsync();
                await Transaction.FetchGasPriceAsync();
                await Transaction.RefreshNonceCommand.ExecuteAsync(null);
                Console.WriteLine($"[DEBUG] Gas loaded. Nonce={Transaction.Transaction.Nonce}, GasLimit={Transaction.Transaction.GasConfiguration?.CustomGasLimit}");
            }
        }
        
        [RelayCommand]
        private void PreviousStep()
        {
            if (CanGoBack)
            {
                CurrentStep--;
                ClearMessages();
            }
        }
        
        [RelayCommand]
        private void SetMaxAmount()
        {
            NativeTransfer.SetMaxAmount();
        }
        
        [RelayCommand]
        private async Task SendTransactionAsync()
        {
            WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"SendTransactionAsync invoked (CurrentStep={CurrentStep})");
            Console.WriteLine($"[DEBUG] SendTransactionAsync called. CurrentStep={CurrentStep}");
            Console.WriteLine($"[DEBUG] CanSendTransaction={CanSendTransaction}");
            
            if (!CanSendTransaction)
            {
                Console.WriteLine($"[DEBUG] Cannot send: CurrentStep={CurrentStep}, NativeTransfer.IsValid={NativeTransfer.IsValid}, Transaction.HasValidTransaction={Transaction.HasValidTransaction()}");
                return;
            }
            
            var validationError = Transaction.ValidateTransaction();
            Console.WriteLine($"[DEBUG] ValidateTransaction result: {validationError ?? "No errors"}");
            
            if (!string.IsNullOrEmpty(validationError))
            {
                Console.WriteLine($"[DEBUG] Validation failed: {validationError}");
                Transaction.ValidationError = validationError;
                ErrorMessage = validationError;
                return;
            }
            
            IsLoading = true;
            ErrorMessage = null;
            SuccessMessage = null;
            
            try
            {
                Console.WriteLine("[DEBUG] Calling Transaction.SendTransactionAsync()...");
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", "SendTransactionAsync calling Transaction.SendTransactionAsync");
                var txHash = await Transaction.SendTransactionAsync();
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"SendTransactionAsync result: {(txHash ?? "null")}");
                Console.WriteLine($"[DEBUG] Transaction result: {txHash ?? "null"}");
                
                if (!string.IsNullOrEmpty(txHash))
                {
                    TransactionHash = txHash;
                    SuccessMessage = _localizer.GetString(SendNativeTokenLocalizer.Keys.TransactionSent);
                    OnTransactionSent?.Invoke(txHash);
                    CurrentStep = 3;
                }
                else
                {
                    ErrorMessage = _localizer.GetString(SendNativeTokenLocalizer.Keys.TransactionFailed);
                    CurrentStep = 2;
                    return;
                }
            }
            catch (Exception ex)
            {
                WalletDiagnosticsLogger.Log("SendNativeTokenViewModel", $"SendTransactionAsync error: {ex.Message}");
                ErrorMessage = $"{_localizer.GetString(SendNativeTokenLocalizer.Keys.TransactionFailed)}: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        [RelayCommand]
        private async Task SimulateTransactionAsync()
        {
            await Transaction.SimulateTransactionCommand.ExecuteAsync(null);
        }
        
        [RelayCommand]
        private void Reset()
        {
            CurrentStep = 1;
            NativeTransfer.Reset();
            ClearMessages();
        }
        
        private void ClearMessages()
        {
            ErrorMessage = null;
            SuccessMessage = null;
            TransactionHash = null;
        }
        
        public void ValidateCurrentStep()
        {
            switch (CurrentStep)
            {
                case 1:
                    NativeTransfer.ValidateAll();
                    break;
                case 2:
                    Transaction.Transaction.GasConfiguration?.ValidateAll();
                    break;
                case 3:
                    NativeTransfer.ValidateAll();
                    break;
            }
        }
        
        public string GetStepTitle() => CurrentStep switch
        {
            1 => _localizer.GetString(SendNativeTokenLocalizer.Keys.Step1Title),
            2 => _localizer.GetString(SendNativeTokenLocalizer.Keys.Step2Title),
            3 => _localizer.GetString(SendNativeTokenLocalizer.Keys.Step3Title),
            _ => ""
        };
        
        public async Task<string?> GetExplorerUrlAsync()
        {
            if (string.IsNullOrEmpty(TransactionHash)) return null;
            
            try
            {
                var chainId = new BigInteger(_walletHostProvider.SelectedNetworkChainId);
                var chain = await _chainManagementService.GetChainAsync(chainId);
                
                if (chain?.Explorers?.Count > 0)
                {
                    var explorerUrl = chain.Explorers.First();
                    return $"{explorerUrl.TrimEnd('/')}/tx/{TransactionHash}";
                }
            }
            catch
            {
            }
            
            return null;
        }
        
        [RelayCommand]
        private async Task ViewTransactionAsync()
        {
            var explorerUrl = await GetExplorerUrlAsync();
            if (!string.IsNullOrEmpty(explorerUrl))
            {
                // Open in browser - implementation would depend on platform
            }
        }
        
        public Action<string>? OnTransactionSent { get; set; }
        public Action? OnExit { get; set; }
    }
}
