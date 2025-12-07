using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using Nethereum.RPC.Chain;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Services.Transactions;
using Nethereum.Wallet.Services;
using Nethereum.Wallet.Storage;

namespace Nethereum.Wallet.UI.Components.Maui.Services
{
    public class MauiPreferencesWalletStorageService : IWalletStorageService
    {
        private const string UserNetworksKey = "wallet.userNetworks";
        private const string SelectedNetworkKey = "wallet.selectedNetwork";
        private const string SelectedAccountKey = "wallet.selectedAccount";
        private const string TransactionsPrefix = "wallet.transactions.";
        private const string RpcSelectionPrefix = "wallet.rpcSelection.";
        private const string ActiveRpcsPrefix = "wallet.activeRpcs.";
        private const string CustomRpcsPrefix = "wallet.customRpcs.";
        private const string RpcHealthPrefix = "wallet.rpcHealth.";
        private const string DappPermissionsIndexKey = "wallet.dappPermissions.index";
        private const string DappPermissionsPrefix = "wallet.dappPermissions.";
        private const string NetworkPreferencePrefix = "wallet.networkPref.";

        private readonly JsonSerializerOptions _jsonOptions;

        public MauiPreferencesWalletStorageService()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            _jsonOptions.Converters.Add(new BigIntegerConverter());
            _jsonOptions.Converters.Add(new JsonStringEnumConverter());
        }

        public async Task<List<ChainFeature>> GetUserNetworksAsync()
        {
            return await GetAsync<List<ChainFeature>>(UserNetworksKey) ?? new List<ChainFeature>();
        }

        public async Task SaveUserNetworksAsync(List<ChainFeature> networks)
        {
            await SaveAsync(UserNetworksKey, networks ?? new List<ChainFeature>());
        }

        public async Task SaveUserNetworkAsync(ChainFeature network)
        {
            var networks = await GetUserNetworksAsync();
            var existing = networks.FirstOrDefault(n => n.ChainId == network.ChainId);
            if (existing != null)
            {
                networks.Remove(existing);
            }
            networks.Add(network);
            await SaveUserNetworksAsync(networks);
        }

        public async Task DeleteUserNetworkAsync(BigInteger chainId)
        {
            var networks = await GetUserNetworksAsync();
            networks.RemoveAll(n => n.ChainId == chainId);
            await SaveUserNetworksAsync(networks);
        }

        public async Task<bool> UserNetworksExistAsync()
        {
            var networks = await GetUserNetworksAsync();
            return networks.Count > 0;
        }

        public Task ClearUserNetworksAsync()
        {
            Preferences.Remove(UserNetworksKey);
            return Task.CompletedTask;
        }

        public async Task SetRpcHealthCacheAsync(string rpcUrl, RpcEndpointHealthCache healthInfo)
        {
            await SaveAsync(GetRpcHealthKey(rpcUrl), healthInfo);
        }

        public Task<RpcEndpointHealthCache?> GetRpcHealthCacheAsync(string rpcUrl)
        {
            return GetAsync<RpcEndpointHealthCache>(GetRpcHealthKey(rpcUrl));
        }

        public Task<List<string>> GetActiveRpcsAsync(BigInteger chainId)
        {
            return Task.FromResult(GetStringList(ActiveRpcsPrefix + chainId));
        }

        public Task SetActiveRpcsAsync(BigInteger chainId, List<string> rpcUrls)
        {
            SetStringList(ActiveRpcsPrefix + chainId, rpcUrls);
            return Task.CompletedTask;
        }

        public async Task RemoveRpcAsync(BigInteger chainId, string rpcUrl)
        {
            var rpcs = await GetActiveRpcsAsync(chainId);
            if (rpcs.Remove(rpcUrl))
            {
                await SetActiveRpcsAsync(chainId, rpcs);
            }
        }

        public Task<List<string>> GetCustomRpcsAsync(BigInteger chainId)
        {
            return Task.FromResult(GetStringList(CustomRpcsPrefix + chainId));
        }

        public async Task SaveCustomRpcAsync(BigInteger chainId, string rpcUrl)
        {
            var rpcs = await GetCustomRpcsAsync(chainId);
            if (!rpcs.Contains(rpcUrl))
            {
                rpcs.Add(rpcUrl);
                SetStringList(CustomRpcsPrefix + chainId, rpcs);
            }
        }

        public async Task RemoveCustomRpcAsync(BigInteger chainId, string rpcUrl)
        {
            var rpcs = await GetCustomRpcsAsync(chainId);
            if (rpcs.Remove(rpcUrl))
            {
                SetStringList(CustomRpcsPrefix + chainId, rpcs);
            }
        }

        public Task SetSelectedNetworkAsync(long chainId)
        {
            Preferences.Set(SelectedNetworkKey, chainId);
            return Task.CompletedTask;
        }

        public Task<long?> GetSelectedNetworkAsync()
        {
            if (!Preferences.ContainsKey(SelectedNetworkKey))
            {
                return Task.FromResult<long?>(null);
            }

            var value = Preferences.Get(SelectedNetworkKey, -1L);
            return Task.FromResult(value >= 0 ? value : (long?)null);
        }

        public Task SetSelectedAccountAsync(string accountAddress)
        {
            Preferences.Set(SelectedAccountKey, accountAddress);
            return Task.CompletedTask;
        }

        public Task<string?> GetSelectedAccountAsync()
        {
            if (!Preferences.ContainsKey(SelectedAccountKey))
            {
                return Task.FromResult<string?>(null);
            }

            var value = Preferences.Get(SelectedAccountKey, string.Empty);
            return Task.FromResult(string.IsNullOrWhiteSpace(value) ? null : value);
        }

        public Task<RpcSelectionConfiguration?> GetRpcSelectionConfigAsync(BigInteger chainId)
        {
            return GetAsync<RpcSelectionConfiguration>(RpcSelectionPrefix + chainId);
        }

        public Task SaveRpcSelectionConfigAsync(RpcSelectionConfiguration config)
        {
            return SaveAsync(RpcSelectionPrefix + config.ChainId, config);
        }

        public async Task<List<TransactionInfo>> GetPendingTransactionsAsync(BigInteger chainId)
        {
            var transactions = await LoadTransactionsAsync(chainId);
            return transactions
                .Where(t => t.Status == TransactionStatus.Pending || t.Status == TransactionStatus.Mining)
                .OrderByDescending(t => t.SubmittedAt)
                .ToList();
        }

        public async Task<List<TransactionInfo>> GetRecentTransactionsAsync(BigInteger chainId)
        {
            var transactions = await LoadTransactionsAsync(chainId);
            return transactions
                .Where(t => t.Status != TransactionStatus.Pending && t.Status != TransactionStatus.Mining)
                .OrderByDescending(t => t.SubmittedAt)
                .ToList();
        }

        public async Task SaveTransactionAsync(BigInteger chainId, TransactionInfo transaction)
        {
            var transactions = await LoadTransactionsAsync(chainId);
            transactions.RemoveAll(t => string.Equals(t.Hash, transaction.Hash, StringComparison.OrdinalIgnoreCase));
            transactions.Add(transaction);
            await SaveTransactionsAsync(chainId, transactions);
        }

        public async Task UpdateTransactionStatusAsync(BigInteger chainId, string hash, TransactionStatus status)
        {
            var transactions = await LoadTransactionsAsync(chainId);
            var existing = transactions.FirstOrDefault(t => string.Equals(t.Hash, hash, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Status = status;
                if (status == TransactionStatus.Confirmed && !existing.ConfirmedAt.HasValue)
                {
                    existing.ConfirmedAt = DateTime.UtcNow;
                }
                await SaveTransactionsAsync(chainId, transactions);
            }
        }

        public async Task<TransactionInfo?> GetTransactionByHashAsync(BigInteger chainId, string hash)
        {
            var transactions = await LoadTransactionsAsync(chainId);
            return transactions.FirstOrDefault(t => string.Equals(t.Hash, hash, StringComparison.OrdinalIgnoreCase));
        }

        public async Task DeleteTransactionAsync(BigInteger chainId, string hash)
        {
            var transactions = await LoadTransactionsAsync(chainId);
            if (transactions.RemoveAll(t => string.Equals(t.Hash, hash, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                await SaveTransactionsAsync(chainId, transactions);
            }
        }

        public Task ClearTransactionsAsync(BigInteger chainId)
        {
            Preferences.Remove(GetTransactionsKey(chainId));
            return Task.CompletedTask;
        }

        public async Task<List<DappPermission>> GetDappPermissionsAsync(string? accountAddress = null)
        {
            if (string.IsNullOrWhiteSpace(accountAddress))
            {
                var list = new List<DappPermission>();
                foreach (var account in GetStringList(DappPermissionsIndexKey))
                {
                    list.AddRange(await ReadPermissionsAsync(account).ConfigureAwait(false));
                }
                return list;
            }

            return await ReadPermissionsAsync(accountAddress).ConfigureAwait(false);
        }

        public async Task AddDappPermissionAsync(string accountAddress, string origin)
        {
            if (string.IsNullOrWhiteSpace(accountAddress) || string.IsNullOrWhiteSpace(origin))
            {
                return;
            }

            var account = NormalizeAccount(accountAddress);
            var normalizedOrigin = NormalizeOrigin(origin);
            var permissions = await ReadPermissionsAsync(account).ConfigureAwait(false);

            if (permissions.Any(p => string.Equals(p.Origin, normalizedOrigin, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            permissions.Add(new DappPermission(normalizedOrigin, account, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            await SavePermissionsAsync(account, permissions).ConfigureAwait(false);
            EnsureAccountIndexed(account);
        }

        public async Task RemoveDappPermissionAsync(string accountAddress, string origin)
        {
            if (string.IsNullOrWhiteSpace(accountAddress) || string.IsNullOrWhiteSpace(origin))
            {
                return;
            }

            var account = NormalizeAccount(accountAddress);
            var permissions = await ReadPermissionsAsync(account).ConfigureAwait(false);
            if (permissions.RemoveAll(p => string.Equals(p.Origin, NormalizeOrigin(origin), StringComparison.OrdinalIgnoreCase)) > 0)
            {
                await SavePermissionsAsync(account, permissions).ConfigureAwait(false);
            }
        }

        public Task SaveNetworkPreferenceAsync(string key, bool value)
        {
            Preferences.Set(NetworkPreferencePrefix + key, value);
            return Task.CompletedTask;
        }

        public Task<bool?> GetNetworkPreferenceAsync(string key)
        {
            var fullKey = NetworkPreferencePrefix + key;
            if (!Preferences.ContainsKey(fullKey))
            {
                return Task.FromResult<bool?>(null);
            }

            return Task.FromResult<bool?>(Preferences.Get(fullKey, false));
        }

        private async Task<List<DappPermission>> ReadPermissionsAsync(string accountAddress)
        {
            var key = DappPermissionsPrefix + NormalizeAccount(accountAddress);
            var data = await GetAsync<List<DappPermission>>(key).ConfigureAwait(false);
            return data ?? new List<DappPermission>();
        }

        private Task SavePermissionsAsync(string accountAddress, List<DappPermission> permissions)
        {
            var key = DappPermissionsPrefix + NormalizeAccount(accountAddress);
            return SaveAsync(key, permissions);
        }

        private void EnsureAccountIndexed(string accountAddress)
        {
            var index = GetStringList(DappPermissionsIndexKey);
            var normalized = NormalizeAccount(accountAddress);
            if (!index.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                index.Add(normalized);
                SetStringList(DappPermissionsIndexKey, index);
            }
        }

        private Task SaveAsync<T>(string key, T value)
        {
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            Preferences.Set(key, json);
            return Task.CompletedTask;
        }

        private Task<T?> GetAsync<T>(string key)
        {
            if (!Preferences.ContainsKey(key))
            {
                return Task.FromResult<T?>(default);
            }

            var json = Preferences.Get(key, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Task.FromResult<T?>(default);
            }

            return Task.FromResult(JsonSerializer.Deserialize<T>(json, _jsonOptions));
        }

        private List<string> GetStringList(string key)
        {
            if (!Preferences.ContainsKey(key))
            {
                return new List<string>();
            }

            var json = Preferences.Get(key, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<string>();
            }

            return JsonSerializer.Deserialize<List<string>>(json, _jsonOptions) ?? new List<string>();
        }

        private void SetStringList(string key, List<string> values)
        {
            var json = JsonSerializer.Serialize(values ?? new List<string>(), _jsonOptions);
            Preferences.Set(key, json);
        }

        private async Task<List<TransactionInfo>> LoadTransactionsAsync(BigInteger chainId)
        {
            return await GetAsync<List<TransactionInfo>>(GetTransactionsKey(chainId)) ?? new List<TransactionInfo>();
        }

        private Task SaveTransactionsAsync(BigInteger chainId, List<TransactionInfo> transactions)
        {
            return SaveAsync(GetTransactionsKey(chainId), transactions);
        }

        private static string GetTransactionsKey(BigInteger chainId) => TransactionsPrefix + chainId;

        private static string NormalizeAccount(string account)
            => account.Trim().ToLowerInvariant();

        private static string NormalizeOrigin(string origin)
            => origin.Trim().ToLowerInvariant();

        private static string GetRpcHealthKey(string rpcUrl)
        {
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(rpcUrl));
            return RpcHealthPrefix + encoded;
        }

        private sealed class BigIntegerConverter : JsonConverter<BigInteger>
        {
            public override BigInteger Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var value = reader.GetString();
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return BigInteger.Zero;
                    }

                    if (BigInteger.TryParse(value, out var result))
                    {
                        return result;
                    }
                }
                else if (reader.TokenType == JsonTokenType.Number)
                {
                    if (reader.TryGetInt64(out var longValue))
                    {
                        return new BigInteger(longValue);
                    }
                }

                throw new JsonException("Unable to convert value to BigInteger");
            }

            public override void Write(Utf8JsonWriter writer, BigInteger value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString());
            }
        }
    }
}
