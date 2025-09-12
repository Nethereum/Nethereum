using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DataServices.Chainlist;
using Nethereum.DataServices.Chainlist.Responses;
using Nethereum.RPC.Chain;

namespace Nethereum.Wallet.Services.Network.Strategies
{
    public class ChainListExternalChainFeaturesProvider : IExternalChainFeaturesProvider
    {
        private readonly ChainlistRpcApiService _api;
        private readonly TimeSpan _ttl;
        private readonly SemaphoreSlim _lock = new(1,1);

        private Task<IReadOnlyList<ChainlistChainInfo>>? _allTask;
        private DateTime _loadedAt;
        private readonly ConcurrentDictionary<BigInteger, ChainFeature> _convertedCache = new();

        public ChainListExternalChainFeaturesProvider(
            ChainlistRpcApiService api,
            TimeSpan? ttl = null)
        {
            _api = api;
            _ttl = ttl is { TotalMilliseconds: > 0 } ? ttl.Value : TimeSpan.FromMinutes(30);
        }

        public async Task<ChainFeature?> GetExternalChainAsync(BigInteger chainId)
        {
            if (_convertedCache.TryGetValue(chainId, out var existing))
                return Clone(existing);

            var all = await GetAllInternalAsync().ConfigureAwait(false);
            var info = all.FirstOrDefault(c => c.ChainId == (long)chainId);
            if (info == null) return null;

            var feature = Convert(info);
            _convertedCache[chainId] = feature;
            return Clone(feature);
        }

        public async Task<IReadOnlyList<ChainFeature>> GetExternalChainsAsync(IEnumerable<BigInteger> chainIds)
        {
            var ids = chainIds.Distinct().ToList();
            var all = await GetAllInternalAsync().ConfigureAwait(false);
            var list = new List<ChainFeature>();

            foreach (var id in ids)
            {
                if (_convertedCache.TryGetValue(id, out var cached))
                {
                    list.Add(Clone(cached));
                    continue;
                }

                var info = all.FirstOrDefault(c => c.ChainId == (long)id);
                if (info == null) continue;

                var conv = Convert(info);
                _convertedCache[id] = conv;
                list.Add(Clone(conv));
            }

            return list;
        }

        public async Task<bool> RefreshAsync(BigInteger chainId)
        {
            // Force reload full dataset (simple approach)
            await GetAllInternalAsync(forceReload: true).ConfigureAwait(false);
            _convertedCache.TryRemove(chainId, out _);
            return await GetExternalChainAsync(chainId).ConfigureAwait(false) != null;
        }

        private async Task<IReadOnlyList<ChainlistChainInfo>> GetAllInternalAsync(bool forceReload = false)
        {
            if (!forceReload && _allTask != null && DateTime.UtcNow - _loadedAt < _ttl)
                return await _allTask.ConfigureAwait(false);

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!forceReload && _allTask != null && DateTime.UtcNow - _loadedAt < _ttl)
                    return await _allTask.ConfigureAwait(false);

                _allTask = LoadAllAsync();
                _loadedAt = DateTime.UtcNow;
                return await _allTask.ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<IReadOnlyList<ChainlistChainInfo>> LoadAllAsync()
        {
            try
            {
                var data = await _api.GetAllChainsAsync().ConfigureAwait(false);
                return data ?? new List<ChainlistChainInfo>(0);
            }
            catch
            {
                return Array.Empty<ChainlistChainInfo>();
            }
        }

        private static ChainFeature Convert(ChainlistChainInfo info)
        {
            var httpRpcs = info.Rpc?
                .Where(r => !string.IsNullOrWhiteSpace(r.Url)
                            && !r.Url.StartsWith("ws", StringComparison.OrdinalIgnoreCase))
                .Select(r => r.Url)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList() ?? new List<string>();

            return new ChainFeature
            {
                ChainId = new BigInteger(info.ChainId),
                ChainName = info.Name ?? $"Chain {info.ChainId}",
                IsTestnet = ChainCategories.IsTestnet(new BigInteger(info.ChainId)),
                NativeCurrency = new NativeCurrency
                {
                    Name = info.NativeCurrency?.Name ?? "",
                    Symbol = info.NativeCurrency?.Symbol ?? "",
                    Decimals = (int)(info.NativeCurrency?.Decimals ?? 18)
                },
                HttpRpcs = httpRpcs,
                WsRpcs = new List<string>(),
                Explorers = info.Explorers?.Select(e => e.Url).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct().ToList()
                            ?? new List<string>(),
                SupportEIP1559 = info.Features?.Any(f => f.Name == "EIP1559") ?? false,
                SupportEIP155 = true
            };
        }

        private static ChainFeature Clone(ChainFeature c) => new()
        {
            ChainId = c.ChainId,
            ChainName = c.ChainName,
            IsTestnet = c.IsTestnet,
            NativeCurrency = c.NativeCurrency == null ? null : new NativeCurrency
            {
                Name = c.NativeCurrency.Name,
                Symbol = c.NativeCurrency.Symbol,
                Decimals = c.NativeCurrency.Decimals
            },
            SupportEIP155 = c.SupportEIP155,
            SupportEIP1559 = c.SupportEIP1559,
            HttpRpcs = c.HttpRpcs?.ToList() ?? new List<string>(),
            WsRpcs = c.WsRpcs?.ToList() ?? new List<string>(),
            Explorers = c.Explorers?.ToList() ?? new List<string>()
        };
    }
}