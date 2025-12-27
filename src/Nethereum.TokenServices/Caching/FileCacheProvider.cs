using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Nethereum.TokenServices.Caching
{
    public class FileCacheProvider : ICacheProvider
    {
        private readonly string _baseDirectory;
        private readonly JsonSerializerOptions _jsonOptions;

        public FileCacheProvider(string baseDirectory = null)
        {
            _baseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Nethereum", "TokenServices", "cache")
                : baseDirectory;

            Directory.CreateDirectory(_baseDirectory);

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task<T> GetAsync<T>(string key)
        {
            var path = GetFilePath(key);
            var metaPath = path + ".meta";

            if (!File.Exists(path) || !File.Exists(metaPath))
                return default;

            try
            {
                var metaJson = File.ReadAllText(metaPath);
                var meta = JsonSerializer.Deserialize<CacheMeta>(metaJson, _jsonOptions);

                if (meta.ExpiresAt.HasValue && DateTime.UtcNow > meta.ExpiresAt.Value)
                {
                    await RemoveAsync(key);
                    return default;
                }

                using (var fs = File.OpenRead(path))
                {
                    return await JsonSerializer.DeserializeAsync<T>(fs, _jsonOptions);
                }
            }
            catch
            {
                return default;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            var path = GetFilePath(key);
            var metaPath = path + ".meta";

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var meta = new CacheMeta
            {
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : (DateTime?)null
            };

            using (var fs = File.Create(path))
            {
                await JsonSerializer.SerializeAsync(fs, value, _jsonOptions);
            }

            var metaJson = JsonSerializer.Serialize(meta, _jsonOptions);
            File.WriteAllText(metaPath, metaJson);
        }

        public Task<bool> ExistsAsync(string key)
        {
            var path = GetFilePath(key);
            var metaPath = path + ".meta";

            if (!File.Exists(path) || !File.Exists(metaPath))
                return Task.FromResult(false);

            try
            {
                var metaJson = File.ReadAllText(metaPath);
                var meta = JsonSerializer.Deserialize<CacheMeta>(metaJson, _jsonOptions);

                if (meta.ExpiresAt.HasValue && DateTime.UtcNow > meta.ExpiresAt.Value)
                {
                    File.Delete(path);
                    File.Delete(metaPath);
                    return Task.FromResult(false);
                }

                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public Task RemoveAsync(string key)
        {
            var path = GetFilePath(key);
            var metaPath = path + ".meta";

            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(metaPath)) File.Delete(metaPath);

            return Task.CompletedTask;
        }

        private string GetFilePath(string key)
        {
            var safeKey = key.Replace(':', '_').Replace('/', '_').Replace('\\', '_');
            return Path.Combine(_baseDirectory, safeKey + ".json");
        }

        private class CacheMeta
        {
            public DateTime CreatedAt { get; set; }
            public DateTime? ExpiresAt { get; set; }
        }
    }
}
