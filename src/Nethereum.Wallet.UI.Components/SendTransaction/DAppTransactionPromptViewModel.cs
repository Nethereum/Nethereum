using System;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Wallet.UI.Components.Services;
using Nethereum.Wallet.UI.Components.Core.Localization;
using Nethereum.Wallet.UI.Components.Prompts;
using Nethereum.Wallet.UI.Components.SendTransaction.Components;
using Nethereum.Wallet.UI.Components.SendTransaction.Models;

namespace Nethereum.Wallet.UI.Components.SendTransaction
{
    public partial class DAppTransactionPromptViewModel : ObservableObject, IDisposable
    {
        private readonly TransactionViewModel _transactionViewModel;
        private readonly IComponentLocalizer<DAppTransactionPromptViewModel> _localizer;
        private TransactionModel? _observedTransactionModel;
        
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
            IComponentLocalizer<DAppTransactionPromptViewModel> localizer)
        {
            _transactionViewModel = transactionViewModel;
            _localizer = localizer;
            _promptInfo = new TransactionPromptInfo();

            _transactionViewModel.PropertyChanged += OnTransactionViewModelPropertyChanged;
            AttachTransactionModelHandlers(_transactionViewModel.Transaction);
        }

        public async Task InitializeAsync(TransactionPromptInfo promptInfo)
        {
            PromptInfo = promptInfo;

            await _transactionViewModel.InitializeFromTransactionInput(promptInfo.TransactionInput);

            OnPropertyChanged(nameof(CanProceedToNextStep));
            OnPropertyChanged(nameof(CanApprove));
        }
        
        [RelayCommand]
        private void NextStep()
        {
            if (CurrentStep < 1)
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
        public async Task ApproveAndSendTransactionAsync()
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
                    ErrorMessage = _localizer.GetString(DAppTransactionPromptLocalizer.Keys.TransactionFailedToSend);
                    ShowRetry = true;
                    CurrentStep = 1;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = _localizer.GetString(
                    DAppTransactionPromptLocalizer.Keys.TransactionFailedWithReason,
                    ex.Message);
                ShowRetry = true;
                CurrentStep = 1;
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
        
        public string? ValidationError => _transactionViewModel.ValidationError;

        public bool CanProceedToNextStep => CurrentStep switch
        {
            0 => Transaction.HasValidTransaction() && !IsLoading && string.IsNullOrWhiteSpace(_transactionViewModel.ValidationError),
            _ => false
        };

        public bool CanApprove =>
            CurrentStep == 1 &&
            Transaction.HasValidTransaction() &&
            !IsLoading &&
            string.IsNullOrWhiteSpace(_transactionViewModel.ValidationError);

        private bool HasReviewData()
        {
            var tx = Transaction.Transaction;
            return !string.IsNullOrWhiteSpace(tx.RecipientAddress)
                   && (!string.IsNullOrWhiteSpace(tx.Amount) || !string.IsNullOrWhiteSpace(tx.Data));
        }

        private void OnTransactionViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TransactionViewModel.Transaction) && _transactionViewModel.Transaction != null)
            {
                AttachTransactionModelHandlers(_transactionViewModel.Transaction);
            }

            if (e.PropertyName == nameof(TransactionViewModel.ValidationError))
            {
                OnPropertyChanged(nameof(ValidationError));
            }

            OnPropertyChanged(nameof(CanProceedToNextStep));
            OnPropertyChanged(nameof(CanApprove));
        }

        private void OnTransactionModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(CanProceedToNextStep));
            OnPropertyChanged(nameof(CanApprove));
        }

        private void AttachTransactionModelHandlers(TransactionModel model)
        {
            if (_observedTransactionModel != null)
            {
                _observedTransactionModel.PropertyChanged -= OnTransactionModelPropertyChanged;
            }

            _observedTransactionModel = model;
            _observedTransactionModel.PropertyChanged += OnTransactionModelPropertyChanged;
        }

        public void Dispose()
        {
            _transactionViewModel.PropertyChanged -= OnTransactionViewModelPropertyChanged;
            if (_observedTransactionModel != null)
            {
                _observedTransactionModel.PropertyChanged -= OnTransactionModelPropertyChanged;
            }
        }

        partial void OnCurrentStepChanged(int value)
        {
            OnPropertyChanged(nameof(CanProceedToNextStep));
            OnPropertyChanged(nameof(CanApprove));
        }

        partial void OnIsLoadingChanged(bool value)
        {
            OnPropertyChanged(nameof(CanProceedToNextStep));
            OnPropertyChanged(nameof(CanApprove));
        }
    }
}
