using Microsoft.JSInterop;
using Nethereum.RPC.Chain;
using Nethereum.Wallet.Storage;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Services.Transactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Nethereum.Wallet.Services;

namespace Nethereum.Wallet.UI.Components.Blazor.Services
{

    public class LocalStorageWalletStorageService : IWalletStorageService
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly JsonSerializerOptions _jsonOptions;
        private const string SettingsPrefix = "Nethereum.Wallet.Settings.";
        private const string CustomChainsKey = "Nethereum.Wallet.CustomChains";
        private const string UserNetworksKey = "Nethereum.Wallet.UserNetworks";
        private const string RpcHealthKey = "Nethereum.Wallet.RpcHealth.";

        public LocalStorageWalletStorageService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task<T?> GetSettingAsync<T>(string key)
        {
            try
            {
                var fullKey = SettingsPrefix + key;
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", fullKey);
                
                if (string.IsNullOrEmpty(json))
                    return default;

                return JsonSerializer.Deserialize<T>(json, _jsonOptions);
            }
            catch
            {
                return default;
            }
        }

        public async Task SetSettingAsync<T>(string key, T value)
        {
            var fullKey = SettingsPrefix + key;
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", fullKey, json);
        }

        public async Task RemoveSettingAsync(string key)
        {
            var fullKey = SettingsPrefix + key;
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", fullKey);
        }

        public async Task<List<string>> GetActiveRpcsAsync(BigInteger chainId)
        {
            try
            {
                var key = $"Nethereum.Wallet.ActiveRpcs.{chainId}";
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
                
                if (string.IsNullOrEmpty(json))
                    return new List<string>();

                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        public async Task SetActiveRpcsAsync(BigInteger chainId, List<string> rpcUrls)
        {
            try
            {
                var key = $"Nethereum.Wallet.ActiveRpcs.{chainId}";
                var json = JsonSerializer.Serialize(rpcUrls);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, json);
            }
            catch
            {
            }
        }

        public async Task RemoveRpcAsync(BigInteger chainId, string rpcUrl)
        {
            try
            {
                var activeRpcs = await GetActiveRpcsAsync(chainId);
                if (activeRpcs.Remove(rpcUrl))
                {
                    await SetActiveRpcsAsync(chainId, activeRpcs);
                }
                
                var network = await GetUserNetworkAsync(chainId);
                if (network != null)
                {
                    network.HttpRpcs?.Remove(rpcUrl);
                    network.WsRpcs?.Remove(rpcUrl);
                    await SaveUserNetworkAsync(network);
                }
            }
            catch
            {
            }
        }

        private async Task<ChainFeature?> GetUserNetworkAsync(BigInteger chainId)
        {
            var networks = await GetUserNetworksAsync();
            return networks.FirstOrDefault(n => n.ChainId == chainId);
        }

        public async Task<List<string>> GetCustomRpcsAsync(BigInteger chainId)
        {
            try
            {
                var key = $"Nethereum.Wallet.CustomRpcs.{chainId}";
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
                if (string.IsNullOrEmpty(json))
                    return new List<string>();

                return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        public async Task SaveCustomRpcAsync(BigInteger chainId, string rpcUrl)
        {
            var customRpcs = await GetCustomRpcsAsync(chainId);
            if (!customRpcs.Contains(rpcUrl))
            {
                customRpcs.Add(rpcUrl);
                var key = $"Nethereum.Wallet.CustomRpcs.{chainId}";
                var json = JsonSerializer.Serialize(customRpcs);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, json);
            }
        }

        public async Task RemoveCustomRpcAsync(BigInteger chainId, string rpcUrl)
        {
            var customRpcs = await GetCustomRpcsAsync(chainId);
            if (customRpcs.Remove(rpcUrl))
            {
                var key = $"Nethereum.Wallet.CustomRpcs.{chainId}";
                var json = JsonSerializer.Serialize(customRpcs);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, json);
            }
        }

        public async Task SetRpcHealthCacheAsync(string rpcUrl, RpcEndpointHealthCache healthInfo)
        {
            var key = RpcHealthKey + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(rpcUrl));
            var json = JsonSerializer.Serialize(healthInfo);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, json);
        }

        public async Task<RpcEndpointHealthCache?> GetRpcHealthCacheAsync(string rpcUrl)
        {
            try
            {
                var key = RpcHealthKey + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(rpcUrl));
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
                
                if (string.IsNullOrEmpty(json))
                    return null;

                return JsonSerializer.Deserialize<RpcEndpointHealthCache>(json);
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<ChainFeature>> GetUserNetworksAsync()
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", UserNetworksKey);
                
                if (string.IsNullOrEmpty(json))
                    return new List<ChainFeature>();

                var storageItems = JsonSerializer.Deserialize<List<ChainFeatureStorage>>(json) ?? new List<ChainFeatureStorage>();
                return storageItems.Select(s => s.ToChainFeature()).ToList();
            }
            catch
            {
                return new List<ChainFeature>();
            }
        }

        public async Task SaveUserNetworksAsync(List<ChainFeature> networks)
        {
            var storageItems = networks.Select(ChainFeatureStorage.FromChainFeature).ToList();
            var json = JsonSerializer.Serialize(storageItems);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", UserNetworksKey, json);
        }

        public async Task ClearUserNetworksAsync()
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", UserNetworksKey);
        }

        public async Task SaveUserNetworkAsync(ChainFeature network)
        {
            var networks = await GetUserNetworksAsync();
            var existingIndex = networks.FindIndex(n => n.ChainId == network.ChainId);
            
            if (existingIndex >= 0)
            {
                networks[existingIndex] = network;
            }
            else
            {
                networks.Add(network);
            }
            
            await SaveUserNetworksAsync(networks);
        }

        public async Task DeleteUserNetworkAsync(BigInteger chainId)
        {
            var networks = await GetUserNetworksAsync();
            var networkToRemove = networks.FirstOrDefault(n => n.ChainId == chainId);
            
            if (networkToRemove != null)
            {
                networks.Remove(networkToRemove);
                await SaveUserNetworksAsync(networks);
            }
        }

        public async Task<bool> UserNetworksExistAsync()
        {
            try
            {
                var networks = await GetUserNetworksAsync();
                return networks.Any();
            }
            catch
            {
                return false;
            }
        }

        public async Task SetSelectedNetworkAsync(long chainId)
        {
            await SetSettingAsync("SelectedNetwork", chainId);
        }

        public async Task<long?> GetSelectedNetworkAsync()
        {
            return await GetSettingAsync<long?>("SelectedNetwork");
        }

        public async Task<List<BigInteger>> GetActiveChainIdsAsync()
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "Nethereum.Wallet.ActiveChains");
                if (string.IsNullOrEmpty(json))
                    return new List<BigInteger>();

                var stringIds = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                return stringIds.Select(id => BigInteger.Parse(id)).ToList();
            }
            catch
            {
                return new List<BigInteger>();
            }
        }

        public async Task SetActiveChainIdsAsync(List<BigInteger> chainIds)
        {
            var stringIds = chainIds.Select(id => id.ToString()).ToList();
            var json = JsonSerializer.Serialize(stringIds);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "Nethereum.Wallet.ActiveChains", json);
        }

        public async Task RemoveCustomChainConfigAsync(BigInteger chainId)
        {
            var key = $"Nethereum.Wallet.CustomChain.{chainId}";
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
        }

        public async Task<string?> GetDAppPermissionsAsync(string dappOrigin)
        {
            try
            {
                var key = $"Nethereum.Wallet.DAppPermissions.{dappOrigin}";
                return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveDAppPermissionsAsync(string dappOrigin, string permissionsJson)
        {
            var key = $"Nethereum.Wallet.DAppPermissions.{dappOrigin}";
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, permissionsJson);
        }

        public async Task RemoveDAppPermissionsAsync(string dappOrigin)
        {
            var key = $"Nethereum.Wallet.DAppPermissions.{dappOrigin}";
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
        }

        public async Task SetSelectedAccountAsync(string accountAddress)
        {
            await SetSettingAsync("SelectedAccount", accountAddress);
        }

        public async Task<string?> GetSelectedAccountAsync()
        {
            return await GetSettingAsync<string?>("SelectedAccount");
        }

        public async Task<RpcSelectionConfiguration?> GetRpcSelectionConfigAsync(BigInteger chainId)
        {
            try
            {
                var key = $"Nethereum.Wallet.RpcSelectionConfig.{chainId}";
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
                
                if (string.IsNullOrEmpty(json))
                    return null;

                return JsonSerializer.Deserialize<RpcSelectionConfiguration>(json, _jsonOptions);
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveRpcSelectionConfigAsync(RpcSelectionConfiguration config)
        {
            try
            {
                if (config == null)
                    throw new ArgumentNullException(nameof(config));

                var key = $"Nethereum.Wallet.RpcSelectionConfig.{config.ChainId}";
                var json = JsonSerializer.Serialize(config, _jsonOptions);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, json);
            }
            catch
            {
            }
        }
        
        private const string TransactionPrefix = "Nethereum.Wallet.Transactions.";
        
        public async Task<List<TransactionInfo>> GetPendingTransactionsAsync(BigInteger chainId)
        {
            try
            {
                var key = $"{TransactionPrefix}Pending.{chainId}";
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
                
                if (string.IsNullOrEmpty(json))
                    return new List<TransactionInfo>();
                    
                return JsonSerializer.Deserialize<List<TransactionInfo>>(json, _jsonOptions) 
                    ?? new List<TransactionInfo>();
            }
            catch
            {
                return new List<TransactionInfo>();
            }
        }
        
        public async Task<List<TransactionInfo>> GetRecentTransactionsAsync(BigInteger chainId)
        {
            try
            {
                var key = $"{TransactionPrefix}Recent.{chainId}";
                var json = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
                
                if (string.IsNullOrEmpty(json))
                    return new List<TransactionInfo>();
                    
                var transactions = JsonSerializer.Deserialize<List<TransactionInfo>>(json, _jsonOptions) 
                    ?? new List<TransactionInfo>();
                    
                return transactions.Take(50).ToList();
            }
            catch
            {
                return new List<TransactionInfo>();
            }
        }
        
        public async Task SaveTransactionAsync(BigInteger chainId, TransactionInfo transaction)
        {
            try
            {
                var pending = await GetPendingTransactionsAsync(chainId);
                pending.Insert(0, transaction);
                
                var pendingKey = $"{TransactionPrefix}Pending.{chainId}";
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", 
                    pendingKey, JsonSerializer.Serialize(pending, _jsonOptions));
            }
            catch
            {
            }
        }
        
        public async Task UpdateTransactionStatusAsync(BigInteger chainId, string hash, TransactionStatus status)
        {
            try
            {
                var pending = await GetPendingTransactionsAsync(chainId);
                var transaction = pending.FirstOrDefault(t => t.Hash == hash);
                
                if (transaction != null)
                {
                    transaction.Status = status;
                    
                    if (status == TransactionStatus.Confirmed || 
                        status == TransactionStatus.Failed || 
                        status == TransactionStatus.Dropped)
                    {
                        pending.Remove(transaction);
                        var recent = await GetRecentTransactionsAsync(chainId);
                        recent.Insert(0, transaction);
                        
                        var pendingKey = $"{TransactionPrefix}Pending.{chainId}";
                        var recentKey = $"{TransactionPrefix}Recent.{chainId}";
                        
                        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", 
                            pendingKey, JsonSerializer.Serialize(pending, _jsonOptions));
                        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", 
                            recentKey, JsonSerializer.Serialize(recent.Take(50).ToList(), _jsonOptions));
                    }
                    else
                    {
                        var pendingKey = $"{TransactionPrefix}Pending.{chainId}";
                        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", 
                            pendingKey, JsonSerializer.Serialize(pending, _jsonOptions));
                    }
                }
            }
            catch
            {
            }
        }
        
        public async Task<TransactionInfo?> GetTransactionByHashAsync(BigInteger chainId, string hash)
        {
            try
            {
                var pending = await GetPendingTransactionsAsync(chainId);
                var transaction = pending.FirstOrDefault(t => t.Hash == hash);
                
                if (transaction != null)
                    return transaction;
                    
                var recent = await GetRecentTransactionsAsync(chainId);
                return recent.FirstOrDefault(t => t.Hash == hash);
            }
            catch
            {
                return null;
            }
        }
        
        public async Task DeleteTransactionAsync(BigInteger chainId, string hash)
        {
            try
            {
                var pending = await GetPendingTransactionsAsync(chainId);
                var transaction = pending.FirstOrDefault(t => t.Hash == hash);
                
                if (transaction != null)
                {
                    pending.Remove(transaction);
                    var pendingKey = $"{TransactionPrefix}Pending.{chainId}";
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", 
                        pendingKey, JsonSerializer.Serialize(pending, _jsonOptions));
                    return;
                }
                
                var recent = await GetRecentTransactionsAsync(chainId);
                transaction = recent.FirstOrDefault(t => t.Hash == hash);
                
                if (transaction != null)
                {
                    recent.Remove(transaction);
                    var recentKey = $"{TransactionPrefix}Recent.{chainId}";
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", 
                        recentKey, JsonSerializer.Serialize(recent, _jsonOptions));
                }
            }
            catch
            {
            }
        }
        
        public async Task ClearTransactionsAsync(BigInteger chainId)
        {
            try
            {
                var pendingKey = $"{TransactionPrefix}Pending.{chainId}";
                var recentKey = $"{TransactionPrefix}Recent.{chainId}";
                
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", pendingKey);
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", recentKey);
            }
            catch
            {
            }
        }

        public Task<List<DappPermission>> GetDappPermissionsAsync(string? accountAddress = null)
        {
            throw new NotImplementedException();
        }

        public Task AddDappPermissionAsync(string accountAddress, string origin)
        {
            throw new NotImplementedException();
        }

        public Task RemoveDappPermissionAsync(string accountAddress, string origin)
        {
            throw new NotImplementedException();
        }
    }
}