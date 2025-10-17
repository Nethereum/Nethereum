using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Uniswap.V4.Positions.PositionManager.ContractDefinition;
using Nethereum.Uniswap.V4.Positions.StateView;
using Nethereum.Util;
using Nethereum.Web3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.Uniswap.V4.Pools
{

    public class PoolCacheService
    {
        private readonly IWeb3 _web3;
        private readonly string _stateViewAddress;
        private readonly string _poolManagerAddress;
        private readonly PoolKeyUtils _poolKeyHelper;
        private readonly IPoolCacheRepository _repository;
        private readonly TimeSpan _cacheExpiration;

        public PoolCacheService(
            IWeb3 web3,
            string stateViewAddress,
            string poolManagerAddress = null,
            IPoolCacheRepository repository = null,
            TimeSpan? cacheExpiration = null
           )
        {
            _web3 = web3;
            _stateViewAddress = stateViewAddress;
            _poolManagerAddress = poolManagerAddress;
            _repository = repository ?? new InMemoryPoolCacheRepository();
            _cacheExpiration = cacheExpiration ?? TimeSpan.FromHours(24);
            _poolKeyHelper = PoolKeyUtils.Current;
        }

        public async Task<PoolCacheEntry> GetOrFetchPoolAsync(
            string currency0,
            string currency1,
            int fee,
            int tickSpacing,
            string hooks = null)
        {
            var normalizedPoolKey = _poolKeyHelper.CreateNormalized(currency0, currency1, fee, tickSpacing, hooks);
            var poolId = CalculatePoolId(normalizedPoolKey);

            var cached = await _repository.GetPoolAsync(poolId).ConfigureAwait(false);
            if (cached != null && (DateTime.UtcNow - cached.LastUpdated) < _cacheExpiration)
            {
                return cached;
            }

            return await FetchAndCachePoolAsync(normalizedPoolKey, poolId).ConfigureAwait(false);
        }

        public async Task<PoolCacheEntry> FetchAndCachePoolAsync(
            PoolKey poolKey,
            string poolId)
        {
            var normalizedPoolKey = _poolKeyHelper.Normalize(poolKey);
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

            var normalizedPoolKey = _poolKeyHelper.CreateNormalized(cached.Currency0, cached.Currency1, cached.Fee, cached.TickSpacing, cached.Hooks);
            return await FetchAndCachePoolAsync(normalizedPoolKey, poolId).ConfigureAwait(false);
        }

       
        public string CalculatePoolId(PoolKey poolKey)
        {
            return CalculatePoolIdBytes(poolKey).ToHex(false);
        }

        public byte[] CalculatePoolIdBytes(PoolKey poolKey)
        {
            var normalized = _poolKeyHelper.Normalize(poolKey);
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
                var normalizedPoolKey = _poolKeyHelper.CreateNormalized(evt.Event.Currency0, evt.Event.Currency1, (int)evt.Event.Fee, evt.Event.TickSpacing, evt.Event.Hooks);

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





