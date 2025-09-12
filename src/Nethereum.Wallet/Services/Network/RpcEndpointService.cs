using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth;
using Nethereum.Wallet.Storage;

namespace Nethereum.Wallet.Services.Network
{
    public class RpcEndpointService : IRpcEndpointService
    {
        private readonly IWalletStorageService _storageService;
        private readonly Dictionary<BigInteger, int> _roundRobinIndexes = new Dictionary<BigInteger, int>();
        private readonly Random _random = new Random();
        private readonly object _lock = new object();
        private readonly TimeSpan _healthCacheExpiry = TimeSpan.FromMinutes(5);
        
        public event EventHandler<RpcHealthChangedEventArgs>? HealthChanged;

        public RpcEndpointService(IWalletStorageService storageService)
        {
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        }

        #region Configuration Management

        public async Task<RpcSelectionConfiguration?> GetConfigurationAsync(BigInteger chainId)
        {
            return await _storageService.GetRpcSelectionConfigAsync(chainId);
        }

        public async Task SaveConfigurationAsync(RpcSelectionConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
                
            if (!config.IsValid())
                throw new ArgumentException("Invalid RPC configuration", nameof(config));
                
            await _storageService.SaveRpcSelectionConfigAsync(config);
        }

        #endregion

        #region Health Management

        public async Task<bool> CheckHealthAsync(string rpcUrl, BigInteger chainId)
        {
            if (string.IsNullOrWhiteSpace(rpcUrl))
                throw new ArgumentException("RPC URL cannot be empty", nameof(rpcUrl));

            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var client = CreateClientForUrl(rpcUrl);

                var ethChainId = new EthChainId(client);
                var actualChainId = await ethChainId.SendRequestAsync();
                
                stopwatch.Stop();
                
                var isHealthy = actualChainId.Value == chainId;
                var responseTimeMs = stopwatch.ElapsedMilliseconds;
                
                var healthCache = new RpcEndpointHealthCache
                {
                    RpcUrl = rpcUrl,
                    IsHealthy = isHealthy,
                    LastChecked = DateTime.UtcNow,
                    ResponseTime = responseTimeMs
                };
                
                await _storageService.SetRpcHealthCacheAsync(rpcUrl, healthCache);
                
                HealthChanged?.Invoke(this, new RpcHealthChangedEventArgs
                {
                    ChainId = chainId,
                    RpcUrl = rpcUrl,
                    IsHealthy = isHealthy,
                    ResponseTimeMs = responseTimeMs,
                    ErrorMessage = isHealthy ? null : $"Chain ID mismatch: expected {chainId}, got {actualChainId.Value}"
                });
                
                return isHealthy;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                var healthCache = new RpcEndpointHealthCache
                {
                    RpcUrl = rpcUrl,
                    IsHealthy = false,
                    LastChecked = DateTime.UtcNow,
                    ResponseTime = stopwatch.ElapsedMilliseconds
                };
                
                await _storageService.SetRpcHealthCacheAsync(rpcUrl, healthCache);
                
                HealthChanged?.Invoke(this, new RpcHealthChangedEventArgs
                {
                    ChainId = chainId,
                    RpcUrl = rpcUrl,
                    IsHealthy = false,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    ErrorMessage = $"Health check failed: {ex.Message}"
                });
                
                return false;
            }
        }

        public async Task<RpcEndpointHealthCache?> GetHealthCacheAsync(string rpcUrl)
        {
            if (string.IsNullOrWhiteSpace(rpcUrl))
                return null;
                
            return await _storageService.GetRpcHealthCacheAsync(rpcUrl);
        }

        public async Task<List<RpcEndpointHealthCache>> GetAllHealthCachesAsync(BigInteger chainId)
        {
            var config = await GetConfigurationAsync(chainId);
            if (config == null || config.SelectedRpcUrls == null || !config.SelectedRpcUrls.Any())
                return new List<RpcEndpointHealthCache>();
            
            var healthCaches = new List<RpcEndpointHealthCache>();
            
            foreach (var url in config.SelectedRpcUrls)
            {
                var health = await GetHealthCacheAsync(url);
                if (health != null)
                {
                    healthCaches.Add(health);
                }
            }
            
            return healthCaches;
        }

        #endregion

        #region Selection Logic

        public async Task<string?> SelectEndpointAsync(BigInteger chainId)
        {
            var config = await GetConfigurationAsync(chainId);
            if (config == null)
                return null;
            
            return await SelectEndpointAsync(chainId, config);
        }

        public async Task<string?> SelectEndpointAsync(BigInteger chainId, RpcSelectionConfiguration config)
        {
            if (config == null || config.SelectedRpcUrls == null || !config.SelectedRpcUrls.Any())
                return null;

            return config.Mode switch
            {
                RpcSelectionMode.Single => config.SelectedRpcUrls.First(),
                RpcSelectionMode.RandomMultiple => await SelectRandomHealthyAsync(config.SelectedRpcUrls),
                RpcSelectionMode.LoadBalanced => await SelectRoundRobinHealthyAsync(chainId, config.SelectedRpcUrls),
                _ => config.SelectedRpcUrls.First()
            };
        }

        public void ResetRoundRobinState(BigInteger chainId)
        {
            lock (_lock)
            {
                _roundRobinIndexes.Remove(chainId);
            }
        }

        #endregion

        #region Private Methods

        private async Task<string> SelectRandomHealthyAsync(List<string> urls)
        {
            var healthyUrls = await GetHealthyUrlsAsync(urls);
            
            if (!healthyUrls.Any())
            {
                return urls.First();
            }

            return healthyUrls[_random.Next(healthyUrls.Count)];
        }

        private async Task<string> SelectRoundRobinHealthyAsync(BigInteger chainId, List<string> urls)
        {
            var healthyUrls = await GetHealthyUrlsAsync(urls);
            
            if (!healthyUrls.Any())
            {
                return urls.First();
            }

            lock (_lock)
            {
                if (!_roundRobinIndexes.TryGetValue(chainId, out var currentIndex))
                {
                    currentIndex = 0;
                }
                
                var selectedIndex = currentIndex % healthyUrls.Count;
                _roundRobinIndexes[chainId] = (currentIndex + 1) % healthyUrls.Count;
                
                return healthyUrls[selectedIndex];
            }
        }

        private async Task<List<string>> GetHealthyUrlsAsync(List<string> urls)
        {
            var healthyUrls = new List<string>();
            var staleThreshold = DateTime.UtcNow.Subtract(_healthCacheExpiry);
            
            foreach (var url in urls)
            {
                var health = await GetHealthCacheAsync(url);
                
                // 1. No health cache exists (never tested - assume healthy)
                // 2. Health cache is stale (needs retest - assume healthy for now)
                if (health == null || 
                    health.LastChecked < staleThreshold || 
                    health.IsHealthy)
                {
                    healthyUrls.Add(url);
                }
            }
            
            return healthyUrls;
        }

        private IClient CreateClientForUrl(string rpcUrl)
        {
            if (string.IsNullOrWhiteSpace(rpcUrl))
                throw new ArgumentException("RPC URL cannot be empty", nameof(rpcUrl));

            if (rpcUrl.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) || 
                rpcUrl.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
            {
                return new WebSocketClient(rpcUrl);
            }
            else
            {
                return new RpcClient(new Uri(rpcUrl));
            }
        }

        #endregion
    }
}