using System;

namespace Nethereum.Wallet.Services.Transactions
{
    public class TransactionEventArgs : EventArgs
    {
        public TransactionInfo Transaction { get; }
        
        public TransactionEventArgs(TransactionInfo transaction)
        {
            Transaction = transaction;
        }
    }
    
    public class TransactionSubmittedEventArgs : TransactionEventArgs
    {
        public TransactionSubmittedEventArgs(TransactionInfo transaction) : base(transaction)
        {
        }
    }
    
    public class TransactionStatusChangedEventArgs : TransactionEventArgs
    {
        public TransactionStatus OldStatus { get; }
        public TransactionStatus NewStatus { get; }
        
        public TransactionStatusChangedEventArgs(TransactionInfo transaction, TransactionStatus oldStatus) 
            : base(transaction)
        {
            OldStatus = oldStatus;
            NewStatus = transaction.Status;
        }
    }
    
    public class TransactionConfirmedEventArgs : TransactionEventArgs
    {
        public TransactionConfirmedEventArgs(TransactionInfo transaction) : base(transaction)
        {
        }
    }
    
    public class TransactionFailedEventArgs : TransactionEventArgs
    {
        public string? Reason { get; }
        
        public TransactionFailedEventArgs(TransactionInfo transaction, string? reason = null) 
            : base(transaction)
        {
            Reason = reason;
        }
    }
}