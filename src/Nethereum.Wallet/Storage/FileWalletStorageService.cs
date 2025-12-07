using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Nethereum.RPC.Chain;
using Nethereum.Wallet.Services.Network;
using Nethereum.Wallet.Services;
using Nethereum.Wallet.Services.Transactions;

namespace Nethereum.Wallet.Storage
{
    public class FileWalletStorageService : IWalletStorageService
    {
        private readonly string _baseDirectory;
        private readonly JsonSerializerOptions _json;

        public FileWalletStorageService(string? baseDirectory = null, JsonSerializerOptions? jsonOptions = null)
        {
            _baseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nethereum", "Wallet")
                : baseDirectory;

            Directory.CreateDirectory(_baseDirectory);

            _json = jsonOptions ?? new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            _json.Converters.Add(new JsonStringEnumConverter());
            _json.Converters.Add(new BigIntegerConverter());
        }

        public async Task<List<ChainFeature>> GetUserNetworksAsync()
            => await ReadAsync<List<ChainFeature>>("user-networks.json") ?? new List<ChainFeature>();

        public Task SaveUserNetworksAsync(List<ChainFeature> networks)
            => WriteAsync("user-networks.json", networks ?? new List<ChainFeature>());

        public async Task SaveUserNetworkAsync(ChainFeature network)
        {
            var list = await GetUserNetworksAsync();
            var existing = list.FirstOrDefault(n => n.ChainId == network.ChainId);
            if (existing != null) list.Remove(existing);
            list.Add(network);
            await SaveUserNetworksAsync(list);
        }

        public async Task DeleteUserNetworkAsync(BigInteger chainId)
        {
            var list = await GetUserNetworksAsync();
            list.RemoveAll(n => n.ChainId == chainId);
            await SaveUserNetworksAsync(list);
        }

        public async Task<bool> UserNetworksExistAsync()
            => (await GetUserNetworksAsync()).Any();

        public Task ClearUserNetworksAsync()
            => DeleteFileAsync("user-networks.json");

        public Task SetRpcHealthCacheAsync(string rpcUrl, RpcEndpointHealthCache healthInfo)
            => WriteAsync(Path.Combine("rpc-health", $"{Encode(rpcUrl)}.json"), healthInfo);

        public Task<RpcEndpointHealthCache?> GetRpcHealthCacheAsync(string rpcUrl)
            => ReadAsync<RpcEndpointHealthCache>(Path.Combine("rpc-health", $"{Encode(rpcUrl)}.json"));

        public Task<List<string>> GetActiveRpcsAsync(BigInteger chainId)
            => ReadAsync<List<string>>(Path.Combine("rpcs", $"active.{chainId}.json"))
               .ContinueWith(t => t.Result ?? new List<string>());

        public Task SetActiveRpcsAsync(BigInteger chainId, List<string> rpcUrls)
            => WriteAsync(Path.Combine("rpcs", $"active.{chainId}.json"), rpcUrls ?? new List<string>());

        public async Task RemoveRpcAsync(BigInteger chainId, string rpcUrl)
        {
            var rpcs = await GetActiveRpcsAsync(chainId);
            if (rpcs.RemoveAll(x => string.Equals(x, rpcUrl, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                await SetActiveRpcsAsync(chainId, rpcs);
            }
        }

        public Task<List<string>> GetCustomRpcsAsync(BigInteger chainId)
            => ReadAsync<List<string>>(Path.Combine("rpcs", $"custom.{chainId}.json"))
               .ContinueWith(t => t.Result ?? new List<string>());

        public async Task SaveCustomRpcAsync(BigInteger chainId, string rpcUrl)
        {
            var rpcs = await GetCustomRpcsAsync(chainId);
            if (!rpcs.Contains(rpcUrl, StringComparer.OrdinalIgnoreCase))
            {
                rpcs.Add(rpcUrl);
                await WriteAsync(Path.Combine("rpcs", $"custom.{chainId}.json"), rpcs);
            }
        }

        public async Task RemoveCustomRpcAsync(BigInteger chainId, string rpcUrl)
        {
            var rpcs = await GetCustomRpcsAsync(chainId);
            if (rpcs.RemoveAll(x => string.Equals(x, rpcUrl, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                await WriteAsync(Path.Combine("rpcs", $"custom.{chainId}.json"), rpcs);
            }
        }

        public Task SetSelectedNetworkAsync(long chainId)
            => WriteAsync("selected-network.json", chainId);

        public Task<long?> GetSelectedNetworkAsync()
            => ReadAsync<long>("selected-network.json").ContinueWith(t => (long?)t.Result);

        public Task SetSelectedAccountAsync(string accountAddress)
            => WriteAsync("selected-account.json", accountAddress ?? "");

        public Task<string?> GetSelectedAccountAsync()
            => ReadAsync<string>("selected-account.json");

        public Task SaveNetworkPreferenceAsync(string key, bool value)
            => WriteAsync(Path.Combine("network-preferences", $"{Encode(key)}.json"), value);

        public Task<bool?> GetNetworkPreferenceAsync(string key)
            => ReadAsync<bool?>(Path.Combine("network-preferences", $"{Encode(key)}.json"));

        public Task<RpcSelectionConfiguration?> GetRpcSelectionConfigAsync(BigInteger chainId)
            => ReadAsync<RpcSelectionConfiguration>(Path.Combine("rpc-selection", $"{chainId}.json"));

        public Task SaveRpcSelectionConfigAsync(RpcSelectionConfiguration config)
            => WriteAsync(Path.Combine("rpc-selection", $"{config.ChainId}.json"), config);

        public Task<List<TransactionInfo>> GetPendingTransactionsAsync(BigInteger chainId)
            => ReadAsync<List<TransactionInfo>>(PendingKey(chainId))
               .ContinueWith(t => t.Result ?? new List<TransactionInfo>());

        public Task<List<TransactionInfo>> GetRecentTransactionsAsync(BigInteger chainId)
            => ReadAsync<List<TransactionInfo>>(RecentKey(chainId))
               .ContinueWith(t => t.Result ?? new List<TransactionInfo>());

        public async Task SaveTransactionAsync(BigInteger chainId, TransactionInfo transaction)
        {
            var pending = await GetPendingTransactionsAsync(chainId);
            pending.RemoveAll(t => string.Equals(t.Hash, transaction.Hash, StringComparison.OrdinalIgnoreCase));
            pending.Insert(0, transaction);
            await WriteAsync(PendingKey(chainId), pending);
        }

        public async Task UpdateTransactionStatusAsync(BigInteger chainId, string hash, TransactionStatus status)
        {
            var pending = await GetPendingTransactionsAsync(chainId);
            var tx = pending.FirstOrDefault(t => string.Equals(t.Hash, hash, StringComparison.OrdinalIgnoreCase));
            if (tx != null)
            {
                tx.Status = status;
                if (status is TransactionStatus.Confirmed && !tx.ConfirmedAt.HasValue)
                    tx.ConfirmedAt = DateTime.UtcNow;

                if (status is TransactionStatus.Confirmed or TransactionStatus.Failed or TransactionStatus.Dropped)
                {
                    pending.Remove(tx);
                    var recent = await GetRecentTransactionsAsync(chainId);
                    recent.Insert(0, tx);
                    await WriteAsync(PendingKey(chainId), pending);
                    await WriteAsync(RecentKey(chainId), recent.Take(50).ToList());
                }
                else
                {
                    await WriteAsync(PendingKey(chainId), pending);
                }
            }
        }

        public async Task<TransactionInfo?> GetTransactionByHashAsync(BigInteger chainId, string hash)
        {
            var pending = await GetPendingTransactionsAsync(chainId);
            var match = pending.FirstOrDefault(t => string.Equals(t.Hash, hash, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            var recent = await GetRecentTransactionsAsync(chainId);
            return recent.FirstOrDefault(t => string.Equals(t.Hash, hash, StringComparison.OrdinalIgnoreCase));
        }

        public async Task DeleteTransactionAsync(BigInteger chainId, string hash)
        {
            var pending = await GetPendingTransactionsAsync(chainId);
            if (pending.RemoveAll(t => string.Equals(t.Hash, hash, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                await WriteAsync(PendingKey(chainId), pending);
                return;
            }

            var recent = await GetRecentTransactionsAsync(chainId);
            if (recent.RemoveAll(t => string.Equals(t.Hash, hash, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                await WriteAsync(RecentKey(chainId), recent);
            }
        }

        public Task ClearTransactionsAsync(BigInteger chainId)
            => Task.WhenAll(DeleteFileAsync(PendingKey(chainId)), DeleteFileAsync(RecentKey(chainId)));

        public async Task<List<DappPermission>> GetDappPermissionsAsync(string? accountAddress = null)
        {
            if (string.IsNullOrWhiteSpace(accountAddress))
            {
                // Aggregate all permissions
                var dir = EnsureDirectory("permissions");
                var list = new List<DappPermission>();
                foreach (var file in Directory.GetFiles(dir, "*.json"))
                {
                    var permissions = await ReadAsync<List<DappPermission>>(Path.Combine("permissions", Path.GetFileName(file))).ConfigureAwait(false);
                    if (permissions != null)
                    {
                        list.AddRange(permissions);
                    }
                }
                return list;
            }

            var normalizedAccount = NormalizeAccount(accountAddress);
            return await ReadAsync<List<DappPermission>>(PermissionKey(normalizedAccount)).ConfigureAwait(false)
                   ?? new List<DappPermission>();
        }

        public async Task AddDappPermissionAsync(string accountAddress, string origin)
        {
            if (string.IsNullOrWhiteSpace(accountAddress) || string.IsNullOrWhiteSpace(origin))
            {
                return;
            }

            var normalizedAccount = NormalizeAccount(accountAddress);
            var normalizedOrigin = NormalizeOrigin(origin);
            var permissions = await GetDappPermissionsAsync(normalizedAccount).ConfigureAwait(false);

            if (permissions.Any(p => string.Equals(p.Origin, normalizedOrigin, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            permissions.Add(new DappPermission(normalizedOrigin, normalizedAccount, DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            await WriteAsync(PermissionKey(normalizedAccount), permissions).ConfigureAwait(false);
        }

        public async Task RemoveDappPermissionAsync(string accountAddress, string origin)
        {
            if (string.IsNullOrWhiteSpace(accountAddress) || string.IsNullOrWhiteSpace(origin))
            {
                return;
            }

            var normalizedAccount = NormalizeAccount(accountAddress);
            var normalizedOrigin = NormalizeOrigin(origin);

            var permissions = await GetDappPermissionsAsync(normalizedAccount).ConfigureAwait(false);
            if (permissions.RemoveAll(p => string.Equals(p.Origin, normalizedOrigin, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                await WriteAsync(PermissionKey(normalizedAccount), permissions).ConfigureAwait(false);
            }
        }

        private string PermissionKey(string accountAddress)
            => Path.Combine("permissions", $"{Encode(accountAddress)}.json");

        private string PendingKey(BigInteger chainId) => Path.Combine("tx", $"pending.{chainId}.json");
        private string RecentKey(BigInteger chainId) => Path.Combine("tx", $"recent.{chainId}.json");

        private async Task<T?> ReadAsync<T>(string relativePath)
        {
            var path = EnsurePath(relativePath);
            if (!File.Exists(path)) return default;
            try
            {
                await using var fs = File.OpenRead(path);
                return await JsonSerializer.DeserializeAsync<T>(fs, _json);
            }
            catch
            {
                return default;
            }
        }

        private async Task WriteAsync<T>(string relativePath, T value)
        {
            var path = EnsurePath(relativePath);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await using var fs = File.Create(path);
            await JsonSerializer.SerializeAsync(fs, value, _json);
        }

        private Task DeleteFileAsync(string relativePath)
        {
            var path = EnsurePath(relativePath);
            if (File.Exists(path)) File.Delete(path);
            return Task.CompletedTask;
        }

        private string EnsurePath(string relativePath)
            => Path.Combine(_baseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));

        private string EnsureDirectory(string relativeDirectory)
        {
            var dir = Path.Combine(_baseDirectory, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static string Encode(string input)
            => Convert.ToBase64String(Encoding.UTF8.GetBytes(input));

        private static string NormalizeAccount(string account)
            => account.Trim().ToLowerInvariant();

        private static string NormalizeOrigin(string origin)
            => origin.Trim().ToLowerInvariant();

        private sealed class BigIntegerConverter : JsonConverter<BigInteger>
        {
            public override BigInteger Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var s = reader.GetString();
                    if (BigInteger.TryParse(s, out var bi)) return bi;
                }
                else if (reader.TokenType == JsonTokenType.Number)
                {
                    if (reader.TryGetInt64(out var n)) return new BigInteger(n);
                }
                throw new JsonException("Invalid BigInteger value");
            }

            public override void Write(Utf8JsonWriter writer, BigInteger value, JsonSerializerOptions options)
                => writer.WriteStringValue(value.ToString());
        }
    }
}
