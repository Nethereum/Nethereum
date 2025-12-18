using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.RPC.Chain;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.Services.Network.Strategies;
using Nethereum.DataServices.Chainlist;
using static Nethereum.Wallet.Services.Network.NetworkInputValidator;

namespace Nethereum.Wallet.Services.Network
{
    public class ChainManagementService : IChainManagementService
    {
        private readonly IWalletStorageService _storageService;
        private readonly IChainFeatureSourceStrategy _strategy;

        private readonly IChainFeaturesService? _chainFeaturesService;

        private readonly ConcurrentDictionary<BigInteger, ChainFeature> _chainCache = new();
        private HashSet<BigInteger> _coreDefaultChainIds = new();

        private const int MaxMergeDistinctCapacity = 32;

        public ChainManagementService(
            IWalletStorageService storageService,
            IChainFeatureSourceStrategy strategy)
        {
            _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        }

        #region IChainManagementService

        public async Task<ChainFeature?> GetCompleteChainAsync(BigInteger chainId)
        {
            if (_chainCache.TryGetValue(chainId, out var cached))
                return CloneChainFeature(cached);

            var userNetworks = await _storageService.GetUserNetworksAsync().ConfigureAwait(false);
            var user = userNetworks.FirstOrDefault(n => n.ChainId == chainId);
            if (user != null)
                return CacheAndClone(user);

            var resolved = await _strategy.ResolveChainAsync(chainId).ConfigureAwait(false);
            if (resolved != null)
                return CacheAndClone(resolved);

            return null;
        }

        public Task<ChainFeature?> GetChainAsync(BigInteger chainId) =>
            GetCompleteChainAsync(chainId);

        public async Task<List<ChainFeature>> GetAllChainsAsync()
        {
            await EnsureUserNetworksInitializedAsync().ConfigureAwait(false);
            var nets = await _storageService.GetUserNetworksAsync().ConfigureAwait(false);
            return nets.Select(CloneChainFeature).ToList();
        }

        public async Task<string?> GetBestRpcEndpointAsync(BigInteger chainId)
        {
            var chain = await GetCompleteChainAsync(chainId).ConfigureAwait(false);
            return chain?.HttpRpcs?.FirstOrDefault();
        }

        public async Task AddCustomChainAsync(ChainFeature chain)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (!NetworkInputValidator.ValidateChainFeature(chain, out var errors))
                throw new ArgumentException($"Invalid chain configuration: {string.Join(',', errors)}", nameof(chain));
            await _storageService.SaveUserNetworkAsync(chain).ConfigureAwait(false);
            _chainCache[chain.ChainId] = chain;
        }

        public async Task<bool> AddCustomNetworkAsync(ChainFeature customNetwork)
        {
            if (customNetwork == null) return false;
            var existing = await _storageService.GetUserNetworksAsync().ConfigureAwait(false);
            if (existing.Any(n => n.ChainId == customNetwork.ChainId)) return false;
            if (!NetworkInputValidator.ValidateChainFeature(customNetwork, out _)) return false;

            await _storageService.SaveUserNetworkAsync(customNetwork).ConfigureAwait(false);
            _chainCache[customNetwork.ChainId] = customNetwork;
            return true;
        }

        public async Task UpdateChainAsync(ChainFeature chain)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (!NetworkInputValidator.ValidateChainFeature(chain, out var errors))
                throw new ArgumentException($"Invalid chain configuration: {string.Join(',', errors)}", nameof(chain));
            await _storageService.SaveUserNetworkAsync(chain).ConfigureAwait(false);
            _chainCache[chain.ChainId] = chain;
        }

        public async Task UpdateChainRpcConfigurationAsync(BigInteger chainId, List<string> httpRpcs, List<string> wsRpcs)
        {
            var userNetworks = await _storageService.GetUserNetworksAsync().ConfigureAwait(false);
            var network = userNetworks.FirstOrDefault(n => n.ChainId == chainId)
                ?? throw new InvalidOperationException($"Network with chainId {chainId} not found");

            network.HttpRpcs = httpRpcs ?? new List<string>();
            network.WsRpcs = wsRpcs ?? new List<string>();

            if (!NetworkInputValidator.ValidateChainFeature(network, out var errors))
                throw new ArgumentException($"Invalid RPC configuration: {string.Join(',', errors)}");

            await _storageService.SaveUserNetworkAsync(network).ConfigureAwait(false);
            _chainCache[chainId] = network;
        }

        public Task RefreshChainDataAsync()
        {
            _chainCache.Clear();
            return Task.CompletedTask;
        }

        public async Task<bool> RefreshRpcsFromChainListAsync(BigInteger chainId)
        {
            var refreshed = await _strategy.RefreshChainAsync(chainId).ConfigureAwait(false);
            var resolved = await _strategy.ResolveChainAsync(chainId).ConfigureAwait(false);
            if (resolved == null) return false;

            var userNetworks = await _storageService.GetUserNetworksAsync().ConfigureAwait(false);
            var user = userNetworks.FirstOrDefault(n => n.ChainId == chainId);
            if (user == null) return false;

            user.HttpRpcs = MergeDistinct(user.HttpRpcs, resolved.HttpRpcs);
            user.WsRpcs = MergeDistinct(user.WsRpcs, resolved.WsRpcs);
            user.Explorers = MergeDistinct(user.Explorers, resolved.Explorers);

            if (!NetworkInputValidator.ValidateChainFeature(user, out _))
                return false;

            await _storageService.SaveUserNetworkAsync(user).ConfigureAwait(false);
            _chainCache[chainId] = user;
            return refreshed || true;
        }

        public async Task<bool> UpdateUserNetworkAsync(ChainFeature updatedNetwork)
        {
            if (updatedNetwork == null) return false;
            if (!NetworkInputValidator.ValidateChainFeature(updatedNetwork, out _)) return false;
            try
            {
                await _storageService.SaveUserNetworkAsync(updatedNetwork).ConfigureAwait(false);
                _chainCache[updatedNetwork.ChainId] = updatedNetwork;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteUserNetworkAsync(BigInteger chainId)
        {
            try
            {
                await _storageService.DeleteUserNetworkAsync(chainId).ConfigureAwait(false);
                _chainCache.TryRemove(chainId, out _);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public Task<List<string>> GetCustomRpcsAsync(BigInteger chainId) =>
            _storageService.GetCustomRpcsAsync(chainId);

        public Task AddCustomRpcAsync(BigInteger chainId, string rpcUrl) =>
            _storageService.SaveCustomRpcAsync(chainId, rpcUrl);

        public Task RemoveCustomRpcAsync(BigInteger chainId, string rpcUrl) =>
            _storageService.RemoveCustomRpcAsync(chainId, rpcUrl);

        public async Task ResetChainToDefaultAsync(BigInteger chainId)
        {
            var defaults = await GetStrategyDefaultCacheAsync().ConfigureAwait(false);
            if (!defaults.TryGetValue(chainId, out var def))
                throw new InvalidOperationException($"No default configuration exists for chainId {chainId}");
            if (!NetworkInputValidator.ValidateChainFeature(def, out var errors))
                throw new ArgumentException($"Default chain configuration invalid: {string.Join(',', errors)}");
            await _storageService.SaveUserNetworkAsync(def).ConfigureAwait(false);
            _chainCache[chainId] = def;
        }

        public bool CanRemoveChain(BigInteger chainId) =>
            !_coreDefaultChainIds.Contains(chainId);

        public async Task RemoveCustomChainAsync(BigInteger chainId)
        {
            if (!CanRemoveChain(chainId))
                throw new InvalidOperationException("Cannot remove core strategy-provided chain");
            await _storageService.DeleteUserNetworkAsync(chainId).ConfigureAwait(false);
            _chainCache.TryRemove(chainId, out _);
        }

        public async Task<ChainFeature?> AddNetworkFromChainListAsync(BigInteger chainId)
        {
            var userNetworks = await _storageService.GetUserNetworksAsync().ConfigureAwait(false);
            if (userNetworks.Any(n => n.ChainId == chainId))
                return null;

            var resolved = await _strategy.ResolveChainAsync(chainId).ConfigureAwait(false);
            if (resolved == null) return null;

            if (!NetworkInputValidator.ValidateChainFeature(resolved, out _)) return null;

            await _storageService.SaveUserNetworkAsync(resolved).ConfigureAwait(false);
            _chainCache[chainId] = resolved;
            return CloneChainFeature(resolved);
        }

        #endregion

        #region Internal helpers

        private async Task EnsureUserNetworksInitializedAsync()
        {
            if (await _storageService.UserNetworksExistAsync().ConfigureAwait(false))
            {
                if (_coreDefaultChainIds.Count == 0)
                    await CacheDefaultIdsAsync().ConfigureAwait(false);
                return;
            }

            var defaults = await _strategy.GetDefaultChainsAsync().ConfigureAwait(false);
            foreach (var d in defaults)
                ApplyKnownChainDefaults(d);

            await _storageService.SaveUserNetworksAsync(defaults).ConfigureAwait(false);

            foreach (var d in defaults)
                _chainCache[d.ChainId] = d;

            _coreDefaultChainIds = defaults.Select(d => d.ChainId).ToHashSet();
        }

        private async Task CacheDefaultIdsAsync()
        {
            var defaults = await _strategy.GetDefaultChainsAsync().ConfigureAwait(false);
            _coreDefaultChainIds = defaults.Select(d => d.ChainId).ToHashSet();
        }

        private async Task<Dictionary<BigInteger, ChainFeature>> GetStrategyDefaultCacheAsync()
        {
            if (_coreDefaultChainIds.Count == 0)
                await CacheDefaultIdsAsync().ConfigureAwait(false);

            var defaults = await _strategy.GetDefaultChainsAsync().ConfigureAwait(false);
            return defaults
                .GroupBy(d => d.ChainId)
                .ToDictionary(g => g.Key, g => CloneChainFeature(g.First()));
        }

        private ChainFeature CacheAndClone(ChainFeature feature)
        {
            ApplyKnownChainDefaults(feature);
            _chainCache[feature.ChainId] = feature;
            return CloneChainFeature(feature);
        }

        private static void ApplyKnownChainDefaults(ChainFeature feature)
        {
            if (ChainCategories.SupportsLightClient(feature.ChainId))
            {
                feature.SupportsLightClient = true;
            }
        }

        private static ChainFeature CloneChainFeature(ChainFeature original) =>
            new()
            {
                ChainId = original.ChainId,
                ChainName = original.ChainName,
                IsTestnet = original.IsTestnet,
                NativeCurrency = original.NativeCurrency == null
                    ? null
                    : new NativeCurrency
                    {
                        Name = original.NativeCurrency.Name,
                        Symbol = original.NativeCurrency.Symbol,
                        Decimals = original.NativeCurrency.Decimals
                    },
                SupportEIP155 = original.SupportEIP155,
                SupportEIP1559 = original.SupportEIP1559,
                HttpRpcs = original.HttpRpcs?.ToList() ?? new List<string>(),
                WsRpcs = original.WsRpcs?.ToList() ?? new List<string>(),
                Explorers = original.Explorers?.ToList() ?? new List<string>(),
                SupportsLightClient = original.SupportsLightClient,
                LightClientEnabled = original.LightClientEnabled,
                BeaconChainApiUrl = original.BeaconChainApiUrl,
                ExecutionRpcUrlForProofs = original.ExecutionRpcUrlForProofs
            };

        private static List<string> MergeDistinct(List<string>? existing, List<string>? incoming)
        {
            if ((existing == null || existing.Count == 0) && (incoming == null || incoming.Count == 0))
                return new List<string>();

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (existing != null)
            {
                foreach (var e in existing)
                {
                    if (!string.IsNullOrWhiteSpace(e))
                        set.Add(e);
                    if (set.Count > MaxMergeDistinctCapacity) break;
                }
            }
            if (incoming != null)
            {
                foreach (var i in incoming)
                {
                    if (!string.IsNullOrWhiteSpace(i))
                        set.Add(i);
                    if (set.Count > MaxMergeDistinctCapacity) break;
                }
            }
            return set.ToList();
        }

        #endregion
    }
}