using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.TokenServices.ERC20;
using Nethereum.TokenServices.ERC20.Pricing;
using Nethereum.Util;
using Nethereum.Wallet.Services.Tokens.Models;
using Nethereum.Wallet.Storage;

namespace Nethereum.Wallet.Services.Tokens
{
    public class PriceRefreshService : IPriceRefreshService
    {
        private const int BatchSize = 200;
        private static readonly TimeSpan JobExpiry = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan PriceStaleThreshold = TimeSpan.FromMinutes(1);

        private readonly ITokenStorageService _tokenStorage;
        private readonly IErc20TokenService _tokenService;
        private readonly ConcurrentDictionary<string, PriceRefreshJob> _jobs = new ConcurrentDictionary<string, PriceRefreshJob>();
        private readonly object _processLock = new object();
        private bool _processing;

        public event EventHandler<PriceRefreshJob> JobProgress;
        public event EventHandler<PriceRefreshJob> JobCompleted;

        public PriceRefreshService(
            ITokenStorageService tokenStorage,
            IErc20TokenService tokenService)
        {
            _tokenStorage = tokenStorage ?? throw new ArgumentNullException(nameof(tokenStorage));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        }

        public int PendingBatchCount => _jobs.Values
            .Where(j => j.Status == PriceRefreshStatus.Queued || j.Status == PriceRefreshStatus.Running)
            .Sum(j => j.Batches.Count(b => b.Status == PriceBatchStatus.Pending));

        public PriceRefreshJob QueuePriceRefresh(string accountAddress, long chainId, string currency = "usd")
        {
            var key = BuildKey(accountAddress, chainId);

            if (_jobs.TryGetValue(key, out var existing) && !existing.IsExpired &&
                (existing.Status == PriceRefreshStatus.Running || existing.Status == PriceRefreshStatus.Queued))
            {
                return existing;
            }

            var job = new PriceRefreshJob
            {
                Id = key,
                AccountAddress = accountAddress,
                ChainId = chainId,
                Currency = currency,
                Status = PriceRefreshStatus.Queued,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(JobExpiry)
            };

            _jobs[key] = job;
            return job;
        }

        public void QueueAllChains(string accountAddress, IEnumerable<long> chainIds, string currency = "usd")
        {
            foreach (var chainId in chainIds)
            {
                QueuePriceRefresh(accountAddress, chainId, currency);
            }
        }

        public async Task<bool> RefreshSingleTokenPriceAsync(string accountAddress, long chainId, string contractAddress, string currency = "usd")
        {
            var data = await _tokenStorage.GetAccountTokenDataAsync(accountAddress, chainId);
            if (data?.Tokens == null) return false;

            var token = data.Tokens.FirstOrDefault(t =>
                string.Equals(t.ContractAddress, contractAddress, StringComparison.OrdinalIgnoreCase));
            if (token == null) return false;

            try
            {
                if (token.IsNative)
                {
                    var nativePrice = await _tokenService.GetNativeTokenPriceAsync(chainId, currency);
                    if (nativePrice == null) return false;

                    token.Price = nativePrice.Price;
                    token.PriceCurrency = currency;
                    token.Value = CalculateValue(token.Balance, token.Decimals, nativePrice.Price);
                    token.PriceLastUpdated = DateTime.UtcNow;
                }
                else
                {
                    var prices = await _tokenService.GetPricesForTokensAsync(chainId, new[] { contractAddress }, currency);
                    if (!prices.TryGetValue(contractAddress.ToLowerInvariant(), out var price))
                        return false;

                    token.Price = price.Price;
                    token.PriceCurrency = currency;
                    token.Value = CalculateValue(token.Balance, token.Decimals, price.Price);
                    token.PriceLastUpdated = DateTime.UtcNow;
                }

                data.LastPriceUpdate = DateTime.UtcNow;
                await _tokenStorage.SaveAccountTokenDataAsync(accountAddress, chainId, data);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<ProcessBatchResult> ProcessNextBatchAsync()
        {
            lock (_processLock)
            {
                if (_processing) return ProcessBatchResult.None;
                _processing = true;
            }

            try
            {
                CleanupExpiredJobs();

                var job = FindNextJob();
                if (job == null) return ProcessBatchResult.None;

                if (job.Status == PriceRefreshStatus.Queued)
                {
                    await InitializeJobBatchesAsync(job);
                    job.Status = PriceRefreshStatus.Running;
                    job.StartedAt = DateTime.UtcNow;
                }

                var batch = job.Batches.FirstOrDefault(b => b.Status == PriceBatchStatus.Pending);
                if (batch == null)
                {
                    CompleteJob(job);
                    return ProcessBatchResult.Success(job.AccountAddress, job.ChainId);
                }

                job.CurrentBatchIndex = batch.Index;

                var data = await _tokenStorage.GetAccountTokenDataAsync(job.AccountAddress, job.ChainId);
                if (data?.Tokens == null || !data.Tokens.Any())
                {
                    CompleteJob(job);
                    return ProcessBatchResult.Success(job.AccountAddress, job.ChainId);
                }

                var success = batch.IsNativeBatch
                    ? await ProcessNativeBatchAsync(job, batch, data)
                    : await ProcessTokenBatchAsync(job, batch, data);

                if (success)
                {
                    await _tokenStorage.SaveAccountTokenDataAsync(job.AccountAddress, job.ChainId, data);
                }

                JobProgress?.Invoke(this, job);

                if (batch.IsRateLimited)
                {
                    return ProcessBatchResult.Limited(job.AccountAddress, job.ChainId);
                }

                if (!job.Batches.Any(b => b.Status == PriceBatchStatus.Pending))
                {
                    CompleteJob(job);
                }

                return ProcessBatchResult.Success(job.AccountAddress, job.ChainId);
            }
            finally
            {
                lock (_processLock)
                {
                    _processing = false;
                }
            }
        }

        public PriceRefreshJob GetJob(string accountAddress, long chainId)
        {
            var key = BuildKey(accountAddress, chainId);
            _jobs.TryGetValue(key, out var job);
            return job;
        }

        public IReadOnlyList<PriceRefreshJob> GetAllJobs()
        {
            return _jobs.Values.ToList().AsReadOnly();
        }

        private async Task InitializeJobBatchesAsync(PriceRefreshJob job)
        {
            var data = await _tokenStorage.GetAccountTokenDataAsync(job.AccountAddress, job.ChainId);
            if (data?.Tokens == null)
            {
                job.TotalTokens = 0;
                job.TotalBatches = 0;
                return;
            }

            var batches = new List<PriceBatch>();
            var batchIndex = 0;
            var cutoff = DateTime.UtcNow - PriceStaleThreshold;

            var nativeToken = data.Tokens.FirstOrDefault(t => t.IsNative);
            var nativeIsStale = nativeToken != null &&
                (!nativeToken.PriceLastUpdated.HasValue || nativeToken.PriceLastUpdated.Value < cutoff);

            if (nativeIsStale)
            {
                batches.Add(new PriceBatch
                {
                    Index = batchIndex++,
                    IsNativeBatch = true,
                    Status = PriceBatchStatus.Pending
                });
            }

            var tokensToPrice = data.Tokens
                .Where(t => !t.IsNative && t.Balance > 0
                    && !string.IsNullOrEmpty(t.ContractAddress)
                    && (!t.PriceLastUpdated.HasValue || t.PriceLastUpdated.Value < cutoff))
                .OrderBy(t => t.PriceLastUpdated ?? DateTime.MinValue)
                .Select(t => t.ContractAddress)
                .ToList();

            for (int i = 0; i < tokensToPrice.Count; i += BatchSize)
            {
                var batchAddresses = tokensToPrice.Skip(i).Take(BatchSize).ToList();
                batches.Add(new PriceBatch
                {
                    Index = batchIndex++,
                    ContractAddresses = batchAddresses,
                    Status = PriceBatchStatus.Pending
                });
            }

            job.Batches = batches;
            job.TotalTokens = (nativeIsStale ? 1 : 0) + tokensToPrice.Count;
            job.TotalBatches = batches.Count;
        }

        private async Task<bool> ProcessNativeBatchAsync(PriceRefreshJob job, PriceBatch batch, AccountTokenData data)
        {
            try
            {
                var nativeToken = data.Tokens.FirstOrDefault(t => t.IsNative);
                if (nativeToken == null)
                {
                    batch.Status = PriceBatchStatus.Completed;
                    batch.ProcessedAt = DateTime.UtcNow;
                    return false;
                }

                var nativePrice = await _tokenService.GetNativeTokenPriceAsync(job.ChainId, job.Currency);
                if (nativePrice != null)
                {
                    nativeToken.Price = nativePrice.Price;
                    nativeToken.PriceCurrency = job.Currency;
                    nativeToken.Value = CalculateValue(nativeToken.Balance, nativeToken.Decimals, nativePrice.Price);
                    nativeToken.PriceLastUpdated = DateTime.UtcNow;
                    data.LastPriceUpdate = DateTime.UtcNow;
                    batch.PricesFound = 1;
                    job.PricedTokens++;
                }
                else
                {
                    nativeToken.PriceLastUpdated = DateTime.UtcNow;
                }

                batch.Status = PriceBatchStatus.Completed;
                batch.ProcessedAt = DateTime.UtcNow;
                return true;
            }
            catch (Exception ex)
            {
                batch.Status = PriceBatchStatus.Failed;
                batch.Error = ex.Message;
                batch.IsRateLimited = IsRateLimitError(ex);
                batch.ProcessedAt = DateTime.UtcNow;
                job.FailedTokens++;
                return false;
            }
        }

        private async Task<bool> ProcessTokenBatchAsync(PriceRefreshJob job, PriceBatch batch, AccountTokenData data)
        {
            try
            {
                var prices = await _tokenService.GetPricesForTokensAsync(
                    job.ChainId, batch.ContractAddresses, job.Currency);

                var pricesFound = 0;
                var now = DateTime.UtcNow;
                foreach (var address in batch.ContractAddresses)
                {
                    var token = data.Tokens.FirstOrDefault(t =>
                        string.Equals(t.ContractAddress, address, StringComparison.OrdinalIgnoreCase));
                    if (token == null) continue;

                    if (prices.TryGetValue(address.ToLowerInvariant(), out var price))
                    {
                        token.Price = price.Price;
                        token.PriceCurrency = job.Currency;
                        token.Value = CalculateValue(token.Balance, token.Decimals, price.Price);
                        token.PriceLastUpdated = now;
                        pricesFound++;
                        job.PricedTokens++;
                    }
                    else
                    {
                        token.PriceLastUpdated = now;
                    }
                }

                batch.PricesFound = pricesFound;
                batch.Status = PriceBatchStatus.Completed;
                batch.ProcessedAt = DateTime.UtcNow;
                data.LastPriceUpdate = DateTime.UtcNow;
                return true;
            }
            catch (Exception ex)
            {
                batch.Status = PriceBatchStatus.Failed;
                batch.Error = ex.Message;
                batch.IsRateLimited = IsRateLimitError(ex);
                batch.ProcessedAt = DateTime.UtcNow;
                job.FailedTokens += batch.ContractAddresses.Count;
                return false;
            }
        }

        private void CompleteJob(PriceRefreshJob job)
        {
            job.Status = job.FailedTokens > 0 && job.PricedTokens == 0
                ? PriceRefreshStatus.Failed
                : PriceRefreshStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            JobCompleted?.Invoke(this, job);
        }

        private PriceRefreshJob FindNextJob()
        {
            return _jobs.Values
                .Where(j => (j.Status == PriceRefreshStatus.Running || j.Status == PriceRefreshStatus.Queued) && !j.IsExpired)
                .OrderBy(j => j.CreatedAt)
                .FirstOrDefault();
        }

        private void CleanupExpiredJobs()
        {
            var expiredKeys = _jobs
                .Where(kvp => kvp.Value.IsExpired && kvp.Value.Status != PriceRefreshStatus.Running)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _jobs.TryRemove(key, out _);
            }
        }

        private static string BuildKey(string accountAddress, long chainId)
        {
            return $"{accountAddress?.ToLowerInvariant()}:{chainId}";
        }

        private static decimal CalculateValue(BigInteger balance, int decimals, decimal price)
        {
            if (balance == 0 || price == 0) return 0;
            try
            {
                var balanceBigDecimal = UnitConversion.Convert.FromWeiToBigDecimal(balance, decimals);
                var value = balanceBigDecimal * (Nethereum.Util.BigDecimal)price;
                return (decimal)value;
            }
            catch (OverflowException)
            {
                return 0;
            }
        }

        private static bool IsRateLimitError(Exception ex)
        {
            var message = ex.Message?.ToLowerInvariant() ?? "";
            return message.Contains("429") || message.Contains("rate limit") || message.Contains("too many requests");
        }
    }
}
