using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Nethereum.TokenServices.Caching;
using Nethereum.Wallet.Services.Tokens.Models;

namespace Nethereum.Wallet.Storage
{
    public class FileTokenStorageService : FileStorageBase, ITokenStorageService
    {
        public FileTokenStorageService(
            string baseDirectory = null,
            JsonSerializerOptions jsonOptions = null,
            Action<string, Exception> onError = null)
            : base(
                baseDirectory ?? GetDefaultDirectory(),
                jsonOptions ?? CreateTokenStorageJsonOptions(),
                onError)
        {
        }

        private static string GetDefaultDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Nethereum", "Wallet", "tokens");
        }

        private static JsonSerializerOptions CreateTokenStorageJsonOptions()
        {
            var options = CreateDefaultJsonOptions();
            options.Converters.Add(new BigIntegerJsonConverter());
            return options;
        }

        public async Task<AccountTokenData> GetAccountTokenDataAsync(string accountAddress, long chainId)
        {
            var path = GetAccountTokenPath(accountAddress, chainId);
            return await ReadAsync<AccountTokenData>(path).ConfigureAwait(false) ?? new AccountTokenData();
        }

        public Task SaveAccountTokenDataAsync(string accountAddress, long chainId, AccountTokenData data)
        {
            var path = GetAccountTokenPath(accountAddress, chainId);
            return WriteAsync(path, data);
        }

        public Task DeleteAccountTokenDataAsync(string accountAddress, long chainId)
        {
            var path = GetAccountTokenPath(accountAddress, chainId);
            return DeleteAsync(path);
        }

        public async Task<List<CustomToken>> GetCustomTokensAsync(long chainId)
        {
            var path = GetCustomTokensPath(chainId);
            return await ReadAsync<List<CustomToken>>(path).ConfigureAwait(false) ?? new List<CustomToken>();
        }

        public async Task AddCustomTokenAsync(long chainId, CustomToken token)
        {
            var tokens = await GetCustomTokensAsync(chainId).ConfigureAwait(false);
            var existing = tokens.FirstOrDefault(t =>
                string.Equals(t.ContractAddress, token.ContractAddress, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                tokens.Remove(existing);
            }

            tokens.Add(token);
            await WriteAsync(GetCustomTokensPath(chainId), tokens).ConfigureAwait(false);
        }

        public async Task UpdateCustomTokenAsync(long chainId, CustomToken token)
        {
            var tokens = await GetCustomTokensAsync(chainId).ConfigureAwait(false);
            var existing = tokens.FirstOrDefault(t =>
                string.Equals(t.ContractAddress, token.ContractAddress, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                tokens.Remove(existing);
                tokens.Add(token);
                await WriteAsync(GetCustomTokensPath(chainId), tokens).ConfigureAwait(false);
            }
        }

        public async Task DeleteCustomTokenAsync(long chainId, string contractAddress)
        {
            var tokens = await GetCustomTokensAsync(chainId).ConfigureAwait(false);
            if (tokens.RemoveAll(t =>
                string.Equals(t.ContractAddress, contractAddress, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                await WriteAsync(GetCustomTokensPath(chainId), tokens).ConfigureAwait(false);
            }
        }

        public async Task<TokenSettings> GetTokenSettingsAsync()
        {
            return await ReadAsync<TokenSettings>("settings.json").ConfigureAwait(false) ?? new TokenSettings();
        }

        public Task SaveTokenSettingsAsync(TokenSettings settings)
        {
            return WriteAsync("settings.json", settings);
        }

        private static string GetAccountTokenPath(string accountAddress, long chainId)
        {
            var normalizedAddress = accountAddress?.Trim().ToLowerInvariant() ?? "";
            return Path.Combine("accounts", normalizedAddress, $"{chainId}.json");
        }

        private static string GetCustomTokensPath(long chainId)
        {
            return Path.Combine("custom", $"{chainId}.json");
        }

        private sealed class BigIntegerJsonConverter : JsonConverter<BigInteger>
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
