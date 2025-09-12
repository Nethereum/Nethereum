using System;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.Wallet.Services.Transactions
{
    public interface IPendingTransactionService
    {
        Task<string> SubmitTransactionAsync(TransactionInfo transaction);
        Task<string> RetryTransactionAsync(TransactionInfo transaction);
        Task<ObservableCollection<TransactionInfo>> GetPendingTransactionsAsync(BigInteger chainId);
        Task<ObservableCollection<TransactionInfo>> GetRecentTransactionsAsync(BigInteger chainId);
        
        event EventHandler<TransactionSubmittedEventArgs>? TransactionSubmitted;
        event EventHandler<TransactionStatusChangedEventArgs>? TransactionStatusChanged;
        event EventHandler<TransactionConfirmedEventArgs>? TransactionConfirmed;
        event EventHandler<TransactionFailedEventArgs>? TransactionFailed;
        
        void StartMonitoring();
        void StopMonitoring();
    }
}