using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Uniswap.V4.PoolManager;
using Nethereum.Uniswap.V4.PoolManager.ContractDefinition;
using Nethereum.Uniswap.V4.StateView;
using Nethereum.Uniswap.V4.StateView.ContractDefinition;
using Nethereum.Util;
using Nethereum.Web3;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Uniswap.V4
{
    public class PoolCacheEntry
    {
        public string PoolId { get; set; }
        public string Currency0 { get; set; }
        public string Currency1 { get; set; }
        public int Fee { get; set; }
        public int TickSpacing { get; set; }
        public string Hooks { get; set; }
        public BigInteger SqrtPriceX96 { get; set; }
        public int Tick { get; set; }
        public DateTime LastUpdated { get; set; }
        public bool Exists { get; set; }
    }

    public interface IV4PoolCacheRepository
    {
        Task<PoolCacheEntry> GetPoolAsync(string poolId);
        Task SavePoolAsync(PoolCacheEntry entry);
        Task<List<PoolCacheEntry>> GetAllPoolsAsync();
        Task ClearAsync();
    }

    public class InMemoryPoolCacheRepository : IV4PoolCacheRepository
    {
        private readonly ConcurrentDictionary<string, PoolCacheEntry> _cache = new ConcurrentDictionary<string, PoolCacheEntry>(StringComparer.OrdinalIgnoreCase);

        public Task<PoolCacheEntry> GetPoolAsync(string poolId)
        {
            _cache.TryGetValue(poolId, out var entry);
            return Task.FromResult(entry);
        }

        public Task SavePoolAsync(PoolCacheEntry entry)
        {
            _cache[entry.PoolId] = entry;
            return Task.CompletedTask;
        }

        public Task<List<PoolCacheEntry>> GetAllPoolsAsync()
        {
            return Task.FromResult(_cache.Values.ToList());
        }

        public Task ClearAsync()
        {
            _cache.Clear();
            return Task.CompletedTask;
        }
    }

    public class V4PoolCache
    {
        private readonly IWeb3 _web3;
        private readonly string _stateViewAddress;
        private readonly string _poolManagerAddress;
        private readonly IV4PoolCacheRepository _repository;
        private readonly TimeSpan _cacheExpiration;

        public V4PoolCache(
            IWeb3 web3,
            string stateViewAddress,
            string poolManagerAddress = null,
            IV4PoolCacheRepository repository = null,
            TimeSpan? cacheExpiration = null)
        {
            _web3 = web3;
            _stateViewAddress = stateViewAddress;
            _poolManagerAddress = poolManagerAddress;
            _repository = repository ?? new InMemoryPoolCacheRepository();
            _cacheExpiration = cacheExpiration ?? TimeSpan.FromHours(24);
        }

        public async Task<PoolCacheEntry> GetOrFetchPoolAsync(
            string currency0,
            string currency1,
            int fee,
            int tickSpacing,
            string hooks = null)
        {
            var normalizedPoolKey = V4PoolKeyHelper.CreateNormalized(currency0, currency1, fee, tickSpacing, hooks);
            var poolId = CalculatePoolId(normalizedPoolKey);

            var cached = await _repository.GetPoolAsync(poolId).ConfigureAwait(false);
            if (cached != null && (DateTime.UtcNow - cached.LastUpdated) < _cacheExpiration)
            {
                return cached;
            }

            return await FetchAndCachePoolAsync(normalizedPoolKey, poolId).ConfigureAwait(false);
        }

        public async Task<PoolCacheEntry> FetchAndCachePoolAsync(
            Nethereum.Uniswap.V4.PositionManager.ContractDefinition.PoolKey poolKey,
            string poolId)
        {
            var normalizedPoolKey = V4PoolKeyHelper.Normalize(poolKey);
            var stateView = new StateViewService(_web3, _stateViewAddress);

            try
            {
                var poolIdBytes = CalculatePoolIdBytes(normalizedPoolKey);
                var slot0 = await stateView.GetSlot0QueryAsync(poolIdBytes).ConfigureAwait(false);

                var entry = new PoolCacheEntry
                {
                    PoolId = poolId,
                    Currency0 = normalizedPoolKey.Currency0,
                    Currency1 = normalizedPoolKey.Currency1,
                    Fee = (int)normalizedPoolKey.Fee,
                    TickSpacing = normalizedPoolKey.TickSpacing,
                    Hooks = normalizedPoolKey.Hooks,
                    SqrtPriceX96 = slot0.SqrtPriceX96,
                    Tick = slot0.Tick,
                    LastUpdated = DateTime.UtcNow,
                    Exists = slot0.SqrtPriceX96 > 0
                };

                await _repository.SavePoolAsync(entry).ConfigureAwait(false);
                return entry;
            }
            catch
            {
                var entry = new PoolCacheEntry
                {
                    PoolId = poolId,
                    Currency0 = normalizedPoolKey.Currency0,
                    Currency1 = normalizedPoolKey.Currency1,
                    Fee = (int)normalizedPoolKey.Fee,
                    TickSpacing = normalizedPoolKey.TickSpacing,
                    Hooks = normalizedPoolKey.Hooks,
                    LastUpdated = DateTime.UtcNow,
                    Exists = false
                };

                return entry;
            }
        }
        public async Task<List<PoolCacheEntry>> GetAllCachedPoolsAsync()
        {
            return await _repository.GetAllPoolsAsync().ConfigureAwait(false);
        }

        public async Task ClearCacheAsync()
        {
            await _repository.ClearAsync().ConfigureAwait(false);
        }

        public async Task<PoolCacheEntry> RefreshPoolAsync(string poolId)
        {
            var cached = await _repository.GetPoolAsync(poolId).ConfigureAwait(false);
            if (cached == null)
            {
                return null;
            }

            var normalizedPoolKey = V4PoolKeyHelper.CreateNormalized(cached.Currency0, cached.Currency1, cached.Fee, cached.TickSpacing, cached.Hooks);
            return await FetchAndCachePoolAsync(normalizedPoolKey, poolId).ConfigureAwait(false);
        }

       
        public static string CalculatePoolId(Nethereum.Uniswap.V4.PositionManager.ContractDefinition.PoolKey poolKey)
        {
            return CalculatePoolIdBytes(poolKey).ToHex(false);
        }

        public static byte[] CalculatePoolIdBytes(Nethereum.Uniswap.V4.PositionManager.ContractDefinition.PoolKey poolKey)
        {
            var normalized = V4PoolKeyHelper.Normalize(poolKey);
            var encoded = normalized.EncodePoolKey();
            return Sha3Keccack.Current.CalculateHash(encoded);
        }

        public async Task<List<PoolCacheEntry>> FindPoolsForTokenAsync(
            string token,
            BigInteger? fromBlockNumber = null,
            BigInteger? toBlockNumber = null,
            CancellationToken cancellationToken = default)
        {
            var cachedPools = await _repository.GetAllPoolsAsync().ConfigureAwait(false);
            var matchingCachedPools = cachedPools
                .Where(p => p.Currency0.Equals(token, StringComparison.OrdinalIgnoreCase) ||
                            p.Currency1.Equals(token, StringComparison.OrdinalIgnoreCase))
                .Where(p => (DateTime.UtcNow - p.LastUpdated) < _cacheExpiration)
                .ToList();

            if (matchingCachedPools.Any())
            {
                return matchingCachedPools;
            }

            if (string.IsNullOrEmpty(_poolManagerAddress))
            {
                throw new InvalidOperationException("PoolManager address is required for event-based pool discovery");
            }

            var poolManager = new Contracts.PoolManager.PoolManagerService(_web3, _poolManagerAddress);

            var events = await poolManager.GetInitializeEventDTOAsync(
                token,
                fromBlockNumber,
                toBlockNumber,
                cancellationToken).ConfigureAwait(false);

            var pools = new List<PoolCacheEntry>();

            foreach (var evt in events)
            {
                var poolId = evt.Event.Id.ToHex(false);
                var normalizedPoolKey = V4PoolKeyHelper.CreateNormalized(evt.Event.Currency0, evt.Event.Currency1, (int)evt.Event.Fee, evt.Event.TickSpacing, evt.Event.Hooks);

                var entry = new PoolCacheEntry
                {
                    PoolId = poolId,
                    Currency0 = normalizedPoolKey.Currency0,
                    Currency1 = normalizedPoolKey.Currency1,
                    Fee = (int)normalizedPoolKey.Fee,
                    TickSpacing = normalizedPoolKey.TickSpacing,
                    Hooks = normalizedPoolKey.Hooks,
                    SqrtPriceX96 = evt.Event.SqrtPriceX96,
                    Tick = evt.Event.Tick,
                    LastUpdated = DateTime.UtcNow,
                    Exists = evt.Event.SqrtPriceX96 > 0
                };

                await _repository.SavePoolAsync(entry).ConfigureAwait(false);
                pools.Add(entry);
            }

            return pools;
        }
    }
}

