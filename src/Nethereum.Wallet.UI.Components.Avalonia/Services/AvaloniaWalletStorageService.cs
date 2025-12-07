using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Nethereum.RPC.Chain;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Services.Transactions;
using Nethereum.Wallet.Services;
using Nethereum.Wallet.Storage;

namespace Nethereum.Wallet.UI.Components.Avalonia.Services
{
    public class AvaloniaWalletStorageService : IWalletStorageService
    {
        private readonly string _storagePath;
        private readonly JsonSerializerOptions _jsonOptions;
        private const string DappPermissionsPrefix = "dapp-permissions-";
        private const string PermissionsIndexFile = "dapp-permissions-index.json";

        public AvaloniaWalletStorageService()
        {
            _storagePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NethereumWallet");
            Directory.CreateDirectory(_storagePath);

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            _jsonOptions.Converters.Add(new JsonStringEnumConverter());
            _jsonOptions.Converters.Add(new BigIntegerConverter());
        }

        public async Task<List<ChainFeature>> GetUserNetworksAsync()
        {
            return await ReadJsonAsync<List<ChainFeature>>("networks.json") ?? new List<ChainFeature>();
        }

        public async Task SaveUserNetworksAsync(List<ChainFeature> networks)
        {
            await WriteJsonAsync("networks.json", networks ?? new List<ChainFeature>());
        }

        public async Task SaveUserNetworkAsync(ChainFeature network)
        {
            var networks = await GetUserNetworksAsync();
            networks.RemoveAll(n => n.ChainId == network.ChainId);
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
            DeleteFile("networks.json");
            return Task.CompletedTask;
        }

        public Task SetRpcHealthCacheAsync(string rpcUrl, RpcEndpointHealthCache healthInfo)
        {
            return WriteJsonAsync(GetRpcHealthFile(rpcUrl), healthInfo);
        }

        public Task<RpcEndpointHealthCache?> GetRpcHealthCacheAsync(string rpcUrl)
        {
            return ReadJsonAsync<RpcEndpointHealthCache>(GetRpcHealthFile(rpcUrl));
        }

        public Task<List<string>> GetActiveRpcsAsync(BigInteger chainId)
        {
            return ReadJsonAsync<List<string>>(GetActiveRpcsFile(chainId))
                .ContinueWith(t => t.Result ?? new List<string>());
        }

        public Task SetActiveRpcsAsync(BigInteger chainId, List<string> rpcUrls)
        {
            return WriteJsonAsync(GetActiveRpcsFile(chainId), rpcUrls ?? new List<string>());
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
            return ReadJsonAsync<List<string>>(GetCustomRpcsFile(chainId))
                .ContinueWith(t => t.Result ?? new List<string>());
        }

        public async Task SaveCustomRpcAsync(BigInteger chainId, string rpcUrl)
        {
            var rpcs = await GetCustomRpcsAsync(chainId);
            if (!rpcs.Contains(rpcUrl))
            {
                rpcs.Add(rpcUrl);
                await WriteJsonAsync(GetCustomRpcsFile(chainId), rpcs);
            }
        }

        public async Task RemoveCustomRpcAsync(BigInteger chainId, string rpcUrl)
        {
            var rpcs = await GetCustomRpcsAsync(chainId);
            if (rpcs.Remove(rpcUrl))
            {
                await WriteJsonAsync(GetCustomRpcsFile(chainId), rpcs);
            }
        }

        public Task SetSelectedNetworkAsync(long chainId)
        {
            return WriteJsonAsync("selected-network.json", chainId);
        }

        public Task<long?> GetSelectedNetworkAsync()
        {
            return ReadJsonAsync<long?>("selected-network.json");
        }

        public Task SetSelectedAccountAsync(string accountAddress)
        {
            return WriteJsonAsync("selected-account.json", accountAddress ?? string.Empty);
        }

        public async Task<string?> GetSelectedAccountAsync()
        {
            var value = await ReadJsonAsync<string>("selected-account.json");
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        public Task SaveNetworkPreferenceAsync(string key, bool value)
        {
            return WriteJsonAsync(GetNetworkPreferenceFile(key), value);
        }

        public Task<bool?> GetNetworkPreferenceAsync(string key)
        {
            return ReadJsonAsync<bool?>(GetNetworkPreferenceFile(key));
        }

        public Task<RpcSelectionConfiguration?> GetRpcSelectionConfigAsync(BigInteger chainId)
        {
            return ReadJsonAsync<RpcSelectionConfiguration>(GetRpcSelectionFile(chainId));
        }

        public Task SaveRpcSelectionConfigAsync(RpcSelectionConfiguration config)
        {
            return WriteJsonAsync(GetRpcSelectionFile(config.ChainId), config);
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
            DeleteFile(GetTransactionsFile(chainId));
            return Task.CompletedTask;
        }

        public async Task<List<DappPermission>> GetDappPermissionsAsync(string? accountAddress = null)
        {
            if (string.IsNullOrWhiteSpace(accountAddress))
            {
                var list = new List<DappPermission>();
                foreach (var account in await LoadPermissionIndexAsync().ConfigureAwait(false))
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
            await WriteJsonAsync(GetPermissionFile(account), permissions).ConfigureAwait(false);
            await EnsureAccountIndexedAsync(account).ConfigureAwait(false);
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
                await WriteJsonAsync(GetPermissionFile(account), permissions).ConfigureAwait(false);
            }
        }

        private Task<List<TransactionInfo>> LoadTransactionsAsync(BigInteger chainId)
        {
            return ReadJsonAsync<List<TransactionInfo>>(GetTransactionsFile(chainId))
                .ContinueWith(t => t.Result ?? new List<TransactionInfo>());
        }

        private Task SaveTransactionsAsync(BigInteger chainId, List<TransactionInfo> transactions)
        {
            return WriteJsonAsync(GetTransactionsFile(chainId), transactions ?? new List<TransactionInfo>());
        }

        private Task<T?> ReadJsonAsync<T>(string fileName)
        {
            var path = Path.Combine(_storagePath, fileName);
            if (!File.Exists(path))
            {
                return Task.FromResult<T?>(default);
            }

            using var stream = File.OpenRead(path);
            return JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions).AsTask();
        }

        private async Task WriteJsonAsync<T>(string fileName, T value)
        {
            var path = Path.Combine(_storagePath, fileName);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, value, _jsonOptions);
        }

        private void DeleteFile(string fileName)
        {
            var path = Path.Combine(_storagePath, fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static string GetTransactionsFile(BigInteger chainId) => $"transactions-{chainId}.json";
        private static string GetRpcSelectionFile(BigInteger chainId) => $"rpc-selection-{chainId}.json";
        private static string GetActiveRpcsFile(BigInteger chainId) => $"active-rpcs-{chainId}.json";
        private static string GetCustomRpcsFile(BigInteger chainId) => $"custom-rpcs-{chainId}.json";
        private static string GetRpcHealthFile(string rpcUrl)
        {
            var safe = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(rpcUrl));
            return $"rpc-health-{safe}.json";
        }
        private static string GetNetworkPreferenceFile(string key)
        {
            return Path.Combine("network-preferences", $"{Encode(key)}.json");
        }

        private static string NormalizeAccount(string account) => account.Trim().ToLowerInvariant();
        private static string NormalizeOrigin(string origin) => origin.Trim().ToLowerInvariant();
        private string GetPermissionFile(string account) => $"{DappPermissionsPrefix}{Encode(account)}.json";

        private Task<List<DappPermission>> ReadPermissionsAsync(string account)
        {
            return ReadJsonAsync<List<DappPermission>>(GetPermissionFile(NormalizeAccount(account)))
                .ContinueWith(t => t.Result ?? new List<DappPermission>());
        }

        private async Task EnsureAccountIndexedAsync(string account)
        {
            var index = await LoadPermissionIndexAsync().ConfigureAwait(false);
            var normalized = NormalizeAccount(account);
            if (!index.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                index.Add(normalized);
                await WriteJsonAsync(PermissionsIndexFile, index).ConfigureAwait(false);
            }
        }

        private async Task<List<string>> LoadPermissionIndexAsync()
        {
            return await ReadJsonAsync<List<string>>(PermissionsIndexFile).ConfigureAwait(false)
                   ?? new List<string>();
        }

        private static string Encode(string value)
        {
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
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
                else if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var number))
                {
                    return new BigInteger(number);
                }

                throw new JsonException("Unable to parse BigInteger value");
            }

            public override void Write(Utf8JsonWriter writer, BigInteger value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString());
            }
        }
    }
}
