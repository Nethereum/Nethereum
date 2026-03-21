using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.ZkProofs;

namespace Nethereum.PrivacyPools
{
    public class UrlCircuitArtifactSource : ICircuitArtifactSource
    {
        private readonly string _baseUrl;
        private readonly string _cacheDir;
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, byte[]> _wasmCache = new Dictionary<string, byte[]>();
        private readonly Dictionary<string, byte[]> _zkeyCache = new Dictionary<string, byte[]>();
        private readonly Dictionary<string, string> _vkCache = new Dictionary<string, string>();

        public UrlCircuitArtifactSource(string baseUrl, string cacheDir = null, HttpClient httpClient = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _cacheDir = cacheDir;
            _httpClient = httpClient ?? new HttpClient();

            if (_cacheDir != null)
                Directory.CreateDirectory(_cacheDir);
        }

        public async Task<byte[]> GetWasmAsync(string circuitName, CancellationToken cancellationToken = default)
        {
            if (_wasmCache.TryGetValue(circuitName, out var cached))
                return cached;

            var bytes = await FetchOrLoadCacheAsync($"{circuitName}.wasm", cancellationToken);
            _wasmCache[circuitName] = bytes;
            return bytes;
        }

        public async Task<byte[]> GetZkeyAsync(string circuitName, CancellationToken cancellationToken = default)
        {
            if (_zkeyCache.TryGetValue(circuitName, out var cached))
                return cached;

            var bytes = await FetchOrLoadCacheAsync($"{circuitName}.zkey", cancellationToken);
            _zkeyCache[circuitName] = bytes;
            return bytes;
        }

        public string GetVerificationKeyJson(string circuitName)
        {
            if (_vkCache.TryGetValue(circuitName, out var cached))
                return cached;

            var task = FetchOrLoadCacheAsync($"{circuitName}_vk.json", CancellationToken.None);
            task.Wait();
            var json = System.Text.Encoding.UTF8.GetString(task.Result);
            _vkCache[circuitName] = json;
            return json;
        }

        public byte[] GetVerificationKey(string circuitName)
        {
            var json = GetVerificationKeyJson(circuitName);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public bool HasCircuit(string circuitName)
        {
            if (_wasmCache.ContainsKey(circuitName))
                return true;

            if (_cacheDir != null && File.Exists(Path.Combine(_cacheDir, $"{circuitName}.wasm")))
                return true;

            return false;
        }

        public async Task InitializeAsync(params string[] circuitNames)
        {
            foreach (var name in circuitNames)
            {
                await GetWasmAsync(name);
                await GetZkeyAsync(name);
            }
        }

        private async Task<byte[]> FetchOrLoadCacheAsync(string fileName, CancellationToken cancellationToken)
        {
            if (_cacheDir != null)
            {
                var cachePath = Path.Combine(_cacheDir, fileName);
                if (File.Exists(cachePath))
                    return await File.ReadAllBytesAsync(cachePath
#if !NETSTANDARD2_0
                        , cancellationToken
#endif
                    );
            }

            var url = $"{_baseUrl}/{fileName}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync(
#if !NETSTANDARD2_0
                cancellationToken
#endif
            );

            if (_cacheDir != null)
            {
                var cachePath = Path.Combine(_cacheDir, fileName);
                await File.WriteAllBytesAsync(cachePath, bytes
#if !NETSTANDARD2_0
                    , cancellationToken
#endif
                );
            }

            return bytes;
        }
    }
}
