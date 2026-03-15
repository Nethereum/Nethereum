using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Wallet.Services.Tokens.Models;

namespace Nethereum.Wallet.Services.Tokens
{
    public class ProcessBatchResult
    {
        public bool Processed { get; set; }
        public string AccountAddress { get; set; }
        public long ChainId { get; set; }
        public bool RateLimited { get; set; }

        public static ProcessBatchResult None => new ProcessBatchResult();
        public static ProcessBatchResult Success(string account, long chainId) =>
            new ProcessBatchResult { Processed = true, AccountAddress = account, ChainId = chainId };
        public static ProcessBatchResult Limited(string account, long chainId) =>
            new ProcessBatchResult { Processed = true, AccountAddress = account, ChainId = chainId, RateLimited = true };
    }

    public interface IPriceRefreshService
    {
        event EventHandler<PriceRefreshJob> JobProgress;
        event EventHandler<PriceRefreshJob> JobCompleted;

        PriceRefreshJob QueuePriceRefresh(string accountAddress, long chainId, string currency = "usd");
        void QueueAllChains(string accountAddress, IEnumerable<long> chainIds, string currency = "usd");
        Task<bool> RefreshSingleTokenPriceAsync(string accountAddress, long chainId, string contractAddress, string currency = "usd");
        Task<ProcessBatchResult> ProcessNextBatchAsync();

        PriceRefreshJob GetJob(string accountAddress, long chainId);
        IReadOnlyList<PriceRefreshJob> GetAllJobs();
        int PendingBatchCount { get; }
    }
}
