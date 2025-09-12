using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Wallet.UI.Components.Services;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.SendTransaction.Components;

namespace Nethereum.Wallet.UI.Components.SendTransaction
{
    public partial class DAppTransactionPromptViewModel : ObservableObject
    {
        private readonly TransactionViewModel _transactionViewModel;
        private readonly IComponentLocalizer<SendNativeTokenViewModel>? _localizer;
        
        [ObservableProperty] private TransactionPromptInfo _promptInfo;
        [ObservableProperty] private int _currentStep = 0;
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string? _errorMessage;
        [ObservableProperty] private string? _transactionHash;
        [ObservableProperty] private bool _showRetry;
        
        public TransactionViewModel Transaction => _transactionViewModel;
        
        public TransactionStatusViewModel? TransactionStatus => _transactionViewModel.TransactionStatus;
        
        public Action<string>? OnTransactionSent { get; set; }
        public Action? OnRejected { get; set; }
        
        public DAppTransactionPromptViewModel(
            TransactionViewModel transactionViewModel,
            IComponentLocalizer<SendNativeTokenViewModel>? localizer = null)
        {
            _transactionViewModel = transactionViewModel;
            _localizer = localizer;
            _promptInfo = new TransactionPromptInfo();
        }
        
        public async Task InitializeAsync(TransactionPromptInfo promptInfo)
        {
            PromptInfo = promptInfo;
            
            await _transactionViewModel.InitializeFromTransactionInput(promptInfo.TransactionInput);
        }
        
        [RelayCommand]
        private void NextStep()
        {
            if (CurrentStep < 2)
            {
                CurrentStep++;
            }
        }
        
        [RelayCommand]
        private void PreviousStep()
        {
            if (CurrentStep > 0)
            {
                CurrentStep--;
            }
        }
        
        [RelayCommand]
        private async Task ApproveAndSendTransactionAsync()
        {
            IsLoading = true;
            ErrorMessage = null;
            ShowRetry = false;
            
            try
            {
                CurrentStep = 2;
                
                var txHash = await _transactionViewModel.SendTransactionAsync();
                
                if (!string.IsNullOrEmpty(txHash))
                {
                    TransactionHash = txHash;
                    OnTransactionSent?.Invoke(txHash);
                }
                else
                {
                    ErrorMessage = "Transaction failed to send";
                    ShowRetry = true;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Transaction failed: {ex.Message}";
                ShowRetry = true;
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        [RelayCommand]
        private async Task RetryTransactionAsync()
        {
            ErrorMessage = null;
            ShowRetry = false;
            await ApproveAndSendTransactionAsync();
        }
        
        [RelayCommand]
        private void RejectTransaction()
        {
            OnRejected?.Invoke();
        }
        
        [RelayCommand]
        private void CancelWithError()
        {
            OnRejected?.Invoke();
        }
        
        public bool CanProceedToNextStep => CurrentStep switch
        {
            0 => Transaction.HasValidTransaction(),
            1 => Transaction.HasValidTransaction() && !IsLoading,
            _ => false
        };
        
        public bool CanApprove => 
            CurrentStep == 1 && 
            Transaction.HasValidTransaction() && 
            !IsLoading;
    }
}