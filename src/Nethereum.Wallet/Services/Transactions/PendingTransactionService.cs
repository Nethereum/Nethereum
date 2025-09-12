using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Wallet.Hosting;
using Nethereum.Wallet.Storage;

namespace Nethereum.Wallet.Services.Transactions
{
    public class PendingTransactionService : IPendingTransactionService
    {
        private readonly IWalletStorageService _storageService;
        private readonly NethereumWalletHostProvider _walletHostProvider;
        private CancellationTokenSource? _monitoringCancellation;
        
        public event EventHandler<TransactionSubmittedEventArgs>? TransactionSubmitted;
        public event EventHandler<TransactionStatusChangedEventArgs>? TransactionStatusChanged;
        public event EventHandler<TransactionConfirmedEventArgs>? TransactionConfirmed;
        public event EventHandler<TransactionFailedEventArgs>? TransactionFailed;
        
        public PendingTransactionService(
            IWalletStorageService storageService,
            NethereumWalletHostProvider walletHostProvider)
        {
            _storageService = storageService;
            _walletHostProvider = walletHostProvider;
        }
        
        public async Task<string> SubmitTransactionAsync(TransactionInfo transaction)
        {
            var chainId = new BigInteger(_walletHostProvider.SelectedNetworkChainId);
            transaction.ChainId = chainId;
            transaction.SubmittedAt = DateTime.UtcNow;
            
            if (string.IsNullOrEmpty(transaction.DisplayName))
            {
                transaction.DisplayName = transaction.Type == TransactionType.NativeToken
                    ? "Native Token Transfer"
                    : "Transaction";
            }
            
            await _storageService.SaveTransactionAsync(chainId, transaction);
            
            TransactionSubmitted?.Invoke(this, new TransactionSubmittedEventArgs(transaction));
            
            _ = MonitorTransactionAsync(transaction);
            
            return transaction.Hash;
        }
        
        public async Task<string> RetryTransactionAsync(TransactionInfo transaction)
        {
            var web3 = await _walletHostProvider.GetWeb3Async();
            
            var transactionInput = new TransactionInput
            {
                From = transaction.From,
                To = transaction.To,
                Value = new HexBigInteger(BigInteger.Parse(transaction.Value)),
                Data = transaction.Data,
                Nonce = string.IsNullOrEmpty(transaction.Nonce) 
                    ? null 
                    : new HexBigInteger(BigInteger.Parse(transaction.Nonce))
            };
            
            var gasEstimate = await web3.Eth.Transactions.EstimateGas.SendRequestAsync(transactionInput);
            transactionInput.Gas = gasEstimate;
            
            var newHash = await web3.TransactionManager.SendTransactionAsync(transactionInput);
            
            var newTransaction = new TransactionInfo
            {
                Hash = newHash,
                From = transaction.From,
                To = transaction.To,
                Value = transaction.Value,
                Type = transaction.Type,
                ChainId = transaction.ChainId,
                DisplayName = $"{transaction.DisplayName} (Retry)",
                SubmittedAt = DateTime.UtcNow,
                Status = TransactionStatus.Pending
            };
            
            await _storageService.SaveTransactionAsync(transaction.ChainId, newTransaction);
            _ = MonitorTransactionAsync(newTransaction);
            
            return newHash;
        }
        
        public async Task<ObservableCollection<TransactionInfo>> GetPendingTransactionsAsync(BigInteger chainId)
        {
            var transactions = await _storageService.GetPendingTransactionsAsync(chainId);
            return new ObservableCollection<TransactionInfo>(transactions);
        }
        
        public async Task<ObservableCollection<TransactionInfo>> GetRecentTransactionsAsync(BigInteger chainId)
        {
            var transactions = await _storageService.GetRecentTransactionsAsync(chainId);
            return new ObservableCollection<TransactionInfo>(transactions);
        }
        
        public void StartMonitoring()
        {
            if (_monitoringCancellation != null) return;
            
            _monitoringCancellation = new CancellationTokenSource();
            _ = MonitorAllPendingTransactionsAsync(_monitoringCancellation.Token);
        }
        
        public void StopMonitoring()
        {
            _monitoringCancellation?.Cancel();
            _monitoringCancellation?.Dispose();
            _monitoringCancellation = null;
        }
        
        private async Task MonitorAllPendingTransactionsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var chainId = new BigInteger(_walletHostProvider.SelectedNetworkChainId);
                    var pendingTransactions = await _storageService.GetPendingTransactionsAsync(chainId);
                    
                    foreach (var transaction in pendingTransactions)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        _ = MonitorTransactionAsync(transaction);
                    }
                    
                    await Task.Delay(10000, cancellationToken);
                }
                catch
                {
                    await Task.Delay(30000, cancellationToken);
                }
            }
        }
        
        private async Task MonitorTransactionAsync(TransactionInfo transaction)
        {
            var web3 = await _walletHostProvider.GetWeb3Async();
            var maxAttempts = 120;
            var attemptCount = 0;
            
            while ((transaction.Status == TransactionStatus.Pending || transaction.Status == TransactionStatus.Mining) 
                   && attemptCount < maxAttempts)
            {
                try
                {
                    var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transaction.Hash);
                    
                    if (receipt != null)
                    {
                        var oldStatus = transaction.Status;
                        transaction.Status = receipt.Status.Value == 1 
                            ? TransactionStatus.Confirmed 
                            : TransactionStatus.Failed;
                        transaction.ConfirmedAt = DateTime.UtcNow;
                        transaction.GasUsed = receipt.GasUsed.Value.ToString();
                        
                        await _storageService.UpdateTransactionStatusAsync(
                            transaction.ChainId, transaction.Hash, transaction.Status);
                        
                        TransactionStatusChanged?.Invoke(this, new TransactionStatusChangedEventArgs(transaction, oldStatus));
                        
                        if (transaction.Status == TransactionStatus.Confirmed)
                        {
                            TransactionConfirmed?.Invoke(this, new TransactionConfirmedEventArgs(transaction));
                        }
                        else
                        {
                            TransactionFailed?.Invoke(this, new TransactionFailedEventArgs(transaction));
                        }
                        
                        break;
                    }
                    
                    await Task.Delay(5000);
                    attemptCount++;
                }
                catch
                {
                    await Task.Delay(10000);
                    attemptCount++;
                }
            }
            
            if (attemptCount >= maxAttempts && transaction.Status == TransactionStatus.Pending)
            {
                var oldStatus = transaction.Status;
                transaction.Status = TransactionStatus.Dropped;
                await _storageService.UpdateTransactionStatusAsync(
                    transaction.ChainId, transaction.Hash, transaction.Status);
                    
                TransactionStatusChanged?.Invoke(this, new TransactionStatusChangedEventArgs(transaction, oldStatus));
                TransactionFailed?.Invoke(this, new TransactionFailedEventArgs(transaction, "Transaction timeout"));
            }
        }
    }
}