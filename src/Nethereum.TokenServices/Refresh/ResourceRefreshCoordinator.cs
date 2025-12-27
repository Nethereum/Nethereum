using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DataServices.CoinGecko;
using Nethereum.TokenServices.Caching;
using Nethereum.TokenServices.ERC20.Discovery;
using Nethereum.TokenServices.ERC20.Models;

namespace Nethereum.TokenServices.Refresh
{
    public class ResourceRefreshCoordinator : IResourceRefreshCoordinator
    {
        private readonly CoinGeckoApiService _coinGeckoService;
        private readonly ICacheProvider _cacheProvider;
        private readonly EmbeddedTokenListProvider _embeddedProvider;
        private readonly TimeSpan _tokenListExpiry;
        private readonly TimeSpan _platformsExpiry;
        private readonly TimeSpan _minTimeBetweenRefreshes;
        private readonly int _maxRetries;

        private readonly List<RefreshJob> _jobQueue = new List<RefreshJob>();
        private readonly HashSet<string> _jobIds = new HashSet<string>();
        private readonly object _lock = new object();
        private DateTime _lastRefreshAttempt = DateTime.MinValue;

        public event Action<RefreshJob> OnJobCompleted;
        public event Action<RefreshJob, Exception> OnJobFailed;

        public ResourceRefreshCoordinator(
            CoinGeckoApiService coinGeckoService,
            ICacheProvider cacheProvider,
            EmbeddedTokenListProvider embeddedProvider,
            TimeSpan? tokenListExpiry = null,
            TimeSpan? platformsExpiry = null,
            TimeSpan? minTimeBetweenRefreshes = null,
            int maxRetries = 3)
        {
            _coinGeckoService = coinGeckoService ?? throw new ArgumentNullException(nameof(coinGeckoService));
            _cacheProvider = cacheProvider ?? throw new ArgumentNullException(nameof(cacheProvider));
            _embeddedProvider = embeddedProvider ?? throw new ArgumentNullException(nameof(embeddedProvider));
            _tokenListExpiry = tokenListExpiry ?? TimeSpan.FromDays(7);
            _platformsExpiry = platformsExpiry ?? TimeSpan.FromDays(30);
            _minTimeBetweenRefreshes = minTimeBetweenRefreshes ?? TimeSpan.FromSeconds(30);
            _maxRetries = maxRetries;
        }

        public int PendingJobCount
        {
            get
            {
                lock (_lock)
                {
                    return _jobQueue.Count;
                }
            }
        }

        public IReadOnlyList<RefreshJob> GetPendingJobs()
        {
            lock (_lock)
            {
                return _jobQueue.ToList();
            }
        }

        public void QueueJob(RefreshJob job)
        {
            if (job == null) return;

            lock (_lock)
            {
                var jobId = job.GetJobId();

                if (_jobIds.Contains(jobId))
                {
                    var existing = _jobQueue.FirstOrDefault(j => j.GetJobId() == jobId);
                    if (existing != null && job.Priority < existing.Priority)
                    {
                        existing.Priority = job.Priority;
                        SortQueue();
                    }
                    return;
                }

                _jobIds.Add(jobId);
                _jobQueue.Add(job);
                SortQueue();
            }
        }

        private void SortQueue()
        {
            _jobQueue.Sort((a, b) =>
            {
                var priorityCompare = a.Priority.CompareTo(b.Priority);
                if (priorityCompare != 0) return priorityCompare;
                return a.CreatedAt.CompareTo(b.CreatedAt);
            });
        }

        public void QueueStaleResources(IEnumerable<long> activeChainIds)
        {
            var chainIds = activeChainIds?.ToList() ?? new List<long>();

            QueueJob(RefreshJob.ForPlatforms(RefreshPriority.Low));

            foreach (var chainId in chainIds)
            {
                QueueJob(RefreshJob.ForTokenList(chainId, RefreshPriority.Normal));
                QueueJob(RefreshJob.ForCoinMapping(chainId, RefreshPriority.Normal));
            }
        }

        public async Task EnsureChainResourcesAsync(long chainId)
        {
            var hasEmbedded = await _embeddedProvider.SupportsChainAsync(chainId);
            var hasCached = await _cacheProvider.ExistsAsync($"tokenlist:{chainId}");

            if (!hasEmbedded && !hasCached)
            {
                var job = RefreshJob.ForTokenList(chainId, RefreshPriority.Critical);
                await ProcessJobImmediatelyAsync(job);
            }
            else
            {
                QueueJob(RefreshJob.ForTokenList(chainId, RefreshPriority.Normal));
            }

            var hasCoinsCached = await _cacheProvider.ExistsAsync($"coinslist:{chainId}");
            if (!hasCoinsCached)
            {
                QueueJob(RefreshJob.ForCoinMapping(chainId, RefreshPriority.High));
            }
            else
            {
                QueueJob(RefreshJob.ForCoinMapping(chainId, RefreshPriority.Normal));
            }
        }

        private async Task ProcessJobImmediatelyAsync(RefreshJob job)
        {
            try
            {
                await ExecuteJobAsync(job);
                OnJobCompleted?.Invoke(job);
            }
            catch (Exception ex)
            {
                job.LastError = ex.Message;
                job.LastAttempt = DateTime.UtcNow;
                job.RetryCount++;

                if (job.RetryCount < _maxRetries)
                {
                    job.Priority = RefreshPriority.High;
                    QueueJob(job);
                }

                OnJobFailed?.Invoke(job, ex);
                throw;
            }
        }

        public async Task<bool> TryProcessNextJobAsync()
        {
            RefreshJob job;

            lock (_lock)
            {
                if (_jobQueue.Count == 0)
                    return false;

                if (DateTime.UtcNow - _lastRefreshAttempt < _minTimeBetweenRefreshes)
                    return false;

                job = _jobQueue[0];
                _jobQueue.RemoveAt(0);
                _jobIds.Remove(job.GetJobId());
                _lastRefreshAttempt = DateTime.UtcNow;
            }

            try
            {
                await ExecuteJobAsync(job);
                OnJobCompleted?.Invoke(job);
                return true;
            }
            catch (Exception ex)
            {
                job.LastError = ex.Message;
                job.LastAttempt = DateTime.UtcNow;
                job.RetryCount++;

                if (job.RetryCount < _maxRetries)
                {
                    job.Priority = job.Priority == RefreshPriority.Critical
                        ? RefreshPriority.High
                        : RefreshPriority.Normal;
                    QueueJob(job);
                }

                OnJobFailed?.Invoke(job, ex);
                return false;
            }
        }

        private async Task ExecuteJobAsync(RefreshJob job)
        {
            switch (job.Type)
            {
                case RefreshType.Platforms:
                    await RefreshPlatformsAsync();
                    break;

                case RefreshType.TokenList:
                    if (job.ChainId.HasValue)
                        await RefreshTokenListAsync(job.ChainId.Value);
                    break;

                case RefreshType.CoinMapping:
                    if (job.ChainId.HasValue)
                        await RefreshCoinMappingAsync(job.ChainId.Value);
                    break;
            }
        }

        private async Task RefreshPlatformsAsync()
        {
            var platforms = await _coinGeckoService.GetAssetPlatformsAsync();
            if (platforms != null && platforms.Count > 0)
            {
                var mapping = platforms
                    .Where(p => p.ChainIdentifier.HasValue)
                    .ToDictionary(p => p.ChainIdentifier.Value, p => p);

                await _cacheProvider.SetAsync("coingecko:platforms", mapping, _platformsExpiry);
            }
        }

        private async Task RefreshTokenListAsync(long chainId)
        {
            var geckoTokens = await _coinGeckoService.GetTokensForChainAsync(chainId);
            if (geckoTokens != null && geckoTokens.Count > 0)
            {
                var tokens = geckoTokens.Select(t => new TokenInfo
                {
                    Address = t.Address,
                    Symbol = t.Symbol,
                    Name = t.Name,
                    Decimals = t.Decimals,
                    LogoUri = t.LogoURI,
                    ChainId = chainId
                }).ToList();

                await _cacheProvider.SetAsync($"tokenlist:{chainId}", tokens, _tokenListExpiry);
            }
        }

        private async Task RefreshCoinMappingAsync(long chainId)
        {
            var tokensCacheKey = $"tokenlist:{chainId}";
            var tokens = await _cacheProvider.GetAsync<List<TokenInfo>>(tokensCacheKey);

            if (tokens == null || tokens.Count == 0)
            {
                tokens = await _embeddedProvider.GetTokensAsync(chainId);
            }

            if (tokens == null || tokens.Count == 0)
                return;

            var addresses = tokens
                .Where(t => !string.IsNullOrEmpty(t.Address))
                .Select(t => t.Address.ToLowerInvariant())
                .Distinct()
                .ToList();

            if (addresses.Count == 0)
                return;

            var mapping = await _coinGeckoService.FindCoinGeckoIdsAsync(addresses, chainId);
            if (mapping != null && mapping.Count > 0)
            {
                await _cacheProvider.SetAsync($"coinslist:{chainId}", mapping, _tokenListExpiry);
            }
        }
    }
}
