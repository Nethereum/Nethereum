using System;
using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.Wallet.Services.Transactions
{
    public class TransactionInfo
    {
        public string Hash { get; set; } = "";
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public string Value { get; set; } = "";
        public string DisplayName { get; set; } = "";
        
        public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
        public int Confirmations { get; set; } = 0;
        public DateTime SubmittedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        
        public BigInteger ChainId { get; set; }
        
        public TransactionType Type { get; set; }
        
        public string? GasUsed { get; set; }
        public string? GasPrice { get; set; }
        
        public string? ErrorMessage { get; set; }
        
        public string? Data { get; set; }
        public string? Nonce { get; set; }
        
        // Receipt data - populated when fetched from blockchain
        public string? BlockNumber { get; set; }
        public string? BlockHash { get; set; }
        public int? TransactionIndex { get; set; }
        public string? CumulativeGasUsed { get; set; }
        public string? EffectiveGasPrice { get; set; }
        public bool? ReceiptStatus { get; set; }
        public string? ContractAddress { get; set; }
        public string? LogsBloom { get; set; }
        public List<TransactionLog>? Logs { get; set; }

        public string? Input { get; set; }
        public string? MaxFeePerGas { get; set; }
        public string? MaxPriorityFeePerGas { get; set; }
        public string? TransactionType { get; set; }
    }

    public class TransactionLog
    {
        public string Address { get; set; } = "";
        public string[] Topics { get; set; } = Array.Empty<string>();
        public string Data { get; set; } = "";
        public string? LogIndex { get; set; }
        public bool Removed { get; set; }
    }

    public enum TransactionType
    {
        NativeToken,
        GeneralTransaction
    }

    public enum TransactionStatus
    {
        Pending,
        Mining,
        Confirmed,
        Failed,
        Dropped
    }
}