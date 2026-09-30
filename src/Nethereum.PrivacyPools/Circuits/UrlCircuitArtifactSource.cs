using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
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
        private readonly IReadOnlyDictionary<string, string> _expectedHashes;

        public UrlCircuitArtifactSource(string baseUrl, string cacheDir = null,
            HttpClient httpClient = null, IReadOnlyDictionary<string, string> expectedHashes = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _cacheDir = cacheDir;
            _httpClient = httpClient ?? new HttpClient();
            _expectedHashes = expectedHashes ?? CircuitArtifactHashes.Default;

            if (_cacheDir != null)
                Directory.CreateDirectory(_cacheDir);
        }

        public async Task<byte[]> GetWasmAsync(string circuitName, CancellationToken cancellationToken = default)
        {
            var cacheKey = NormalizeCacheKey(circuitName);
            if (_wasmCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var bytes = await FetchOrLoadCacheAsync(GetArtifactFileNames(circuitName, ArtifactKind.Wasm), cancellationToken);
            _wasmCache[cacheKey] = bytes;
            return bytes;
        }

        public async Task<byte[]> GetZkeyAsync(string circuitName, CancellationToken cancellationToken = default)
        {
            var cacheKey = NormalizeCacheKey(circuitName);
            if (_zkeyCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var bytes = await FetchOrLoadCacheAsync(GetArtifactFileNames(circuitName, ArtifactKind.Zkey), cancellationToken);
            _zkeyCache[cacheKey] = bytes;
            return bytes;
        }

        public string GetVerificationKeyJson(string circuitName)
        {
            var cacheKey = NormalizeCacheKey(circuitName);
            if (_vkCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var bytes = FetchOrLoadCacheAsync(GetArtifactFileNames(circuitName, ArtifactKind.VerificationKey),
                CancellationToken.None).GetAwaiter().GetResult();
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            _vkCache[cacheKey] = json;
            return json;
        }

        public byte[] GetVerificationKey(string circuitName)
        {
            var json = GetVerificationKeyJson(circuitName);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public bool HasCircuit(string circuitName)
        {
            var cacheKey = NormalizeCacheKey(circuitName);
            if (_wasmCache.ContainsKey(cacheKey))
                return true;

            if (_cacheDir != null &&
                GetArtifactFileNames(circuitName, ArtifactKind.Wasm)
                    .Any(fileName => File.Exists(Path.Combine(_cacheDir, fileName))))
            {
                return true;
            }

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

        private async Task<byte[]> FetchOrLoadCacheAsync(IEnumerable<string> fileNames, CancellationToken cancellationToken)
        {
            if (_cacheDir != null)
            {
                foreach (var fileName in fileNames)
                {
                    var cachePath = Path.Combine(_cacheDir, fileName);
                    if (!File.Exists(cachePath))
                        continue;

                    var cached = await File.ReadAllBytesAsync(cachePath
#if !NETSTANDARD2_0
                        , cancellationToken
#endif
                    );
                    VerifyIntegrity(cached, fileName);
                    return cached;
                }
            }

            HttpRequestException lastNotFound = null;
            foreach (var fileName in fileNames)
            {
                var url = $"{_baseUrl}/{fileName}";
                using var response = await _httpClient.GetAsync(url, cancellationToken);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    lastNotFound = new HttpRequestException($"Artifact not found: {url}");
                    continue;
                }

                response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync(
#if !NETSTANDARD2_0
                    cancellationToken
#endif
                );

                VerifyIntegrity(bytes, fileName);

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

            if (lastNotFound != null)
                throw lastNotFound;

            throw new InvalidOperationException("No circuit artifact file names were provided.");
        }

        private void VerifyIntegrity(byte[] data, string fileName)
        {
            if (!_expectedHashes.TryGetValue(fileName, out var expectedHash))
                throw new InvalidOperationException(
                    $"No integrity hash registered for '{fileName}'. Refusing to load unverified artifact.");

            using var sha = SHA256.Create();
            var actual = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();

            if (actual != expectedHash.ToLowerInvariant())
                throw new InvalidOperationException(
                    $"Integrity check failed for '{fileName}'. Expected SHA-256: {expectedHash}, got: {actual}");
        }

        private static string NormalizeCacheKey(string circuitName)
        {
            return circuitName.Equals("withdraw", StringComparison.OrdinalIgnoreCase)
                ? "withdrawal"
                : circuitName.ToLowerInvariant();
        }

        private static string[] GetArtifactFileNames(string circuitName, ArtifactKind artifactKind)
        {
            if (circuitName.Equals("withdraw", StringComparison.OrdinalIgnoreCase) ||
                circuitName.Equals("withdrawal", StringComparison.OrdinalIgnoreCase))
            {
                switch (artifactKind)
                {
                    case ArtifactKind.Wasm:
                        return new[] { "withdraw.wasm", "withdrawal.wasm" };
                    case ArtifactKind.Zkey:
                        return new[] { "withdraw.zkey", "withdrawal.zkey" };
                    default:
                        return new[] { "withdraw.vkey", "withdrawal_vk.json" };
                }
            }

            var normalized = circuitName.ToLowerInvariant();
            switch (artifactKind)
            {
                case ArtifactKind.Wasm:
                    return new[] { $"{normalized}.wasm" };
                case ArtifactKind.Zkey:
                    return new[] { $"{normalized}.zkey" };
                default:
                    return new[] { $"{normalized}.vkey", $"{normalized}_vk.json" };
            }
        }

        private enum ArtifactKind
        {
            Wasm,
            Zkey,
            VerificationKey
        }
    }
}
