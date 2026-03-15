using System;
using System.Collections.Generic;

namespace Nethereum.Wallet.Services.Tokens.Models
{
    public enum PriceRefreshStatus { Queued, Running, Completed, Failed, Cancelled }

    public enum PriceBatchStatus { Pending, Completed, Failed, Skipped }

    public class PriceRefreshJob
    {
        public string Id { get; set; }
        public string AccountAddress { get; set; }
        public long ChainId { get; set; }
        public string Currency { get; set; }

        public PriceRefreshStatus Status { get; set; }
        public int TotalTokens { get; set; }
        public int PricedTokens { get; set; }
        public int FailedTokens { get; set; }
        public int CurrentBatchIndex { get; set; }
        public int TotalBatches { get; set; }

        public List<PriceBatch> Batches { get; set; } = new List<PriceBatch>();

        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime ExpiresAt { get; set; }

        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }

    public class PriceBatch
    {
        public int Index { get; set; }
        public List<string> ContractAddresses { get; set; } = new List<string>();
        public bool IsNativeBatch { get; set; }
        public PriceBatchStatus Status { get; set; }
        public int PricesFound { get; set; }
        public string Error { get; set; }
        public bool IsRateLimited { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }
}
