using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Nethereum.PrivacyPools.Circuits;
using Xunit;

namespace Nethereum.PrivacyPools.Tests
{
    public class ArtifactVerificationTests : IDisposable
    {
        private readonly string _cacheDir;

        public ArtifactVerificationTests()
        {
            _cacheDir = Path.Combine(Path.GetTempPath(), $"artifact-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(_cacheDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_cacheDir))
                Directory.Delete(_cacheDir, recursive: true);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-ArtifactVerification")]
        public async Task CorrectHash_LoadsSuccessfully()
        {
            var testBytes = new byte[] { 1, 2, 3, 4, 5 };
            File.WriteAllBytes(Path.Combine(_cacheDir, "test.wasm"), testBytes);

            using var sha = SHA256.Create();
            var hash = BitConverter.ToString(sha.ComputeHash(testBytes))
                .Replace("-", "").ToLowerInvariant();

            var hashes = new Dictionary<string, string> { ["test.wasm"] = hash };
            var source = new UrlCircuitArtifactSource("http://unused",
                cacheDir: _cacheDir, expectedHashes: hashes);

            var result = await source.GetWasmAsync("test");

            Assert.Equal(testBytes, result);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-ArtifactVerification")]
        public async Task WrongHash_ThrowsInvalidOperationException()
        {
            var testBytes = new byte[] { 1, 2, 3, 4, 5 };
            File.WriteAllBytes(Path.Combine(_cacheDir, "test.wasm"), testBytes);

            var hashes = new Dictionary<string, string>
            {
                ["test.wasm"] = "0000000000000000000000000000000000000000000000000000000000000000"
            };
            var source = new UrlCircuitArtifactSource("http://unused",
                cacheDir: _cacheDir, expectedHashes: hashes);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => source.GetWasmAsync("test"));

            Assert.Contains("Integrity check failed", ex.Message);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-ArtifactVerification")]
        public async Task BuiltInHashes_LoadCanonicalArtifactNames()
        {
            var embedded = new PrivacyPoolCircuitSource();

            File.WriteAllBytes(Path.Combine(_cacheDir, "commitment.wasm"),
                await embedded.GetWasmAsync("commitment"));
            File.WriteAllBytes(Path.Combine(_cacheDir, "commitment.zkey"),
                await embedded.GetZkeyAsync("commitment"));
            File.WriteAllText(Path.Combine(_cacheDir, "commitment.vkey"),
                NormalizeToLf(embedded.GetVerificationKeyJson("commitment")),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            File.WriteAllBytes(Path.Combine(_cacheDir, "withdraw.wasm"),
                await embedded.GetWasmAsync("withdrawal"));
            File.WriteAllBytes(Path.Combine(_cacheDir, "withdraw.zkey"),
                await embedded.GetZkeyAsync("withdrawal"));
            File.WriteAllText(Path.Combine(_cacheDir, "withdraw.vkey"),
                NormalizeToLf(embedded.GetVerificationKeyJson("withdrawal")),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var source = new UrlCircuitArtifactSource("http://unused", cacheDir: _cacheDir);

            var commitmentWasm = await source.GetWasmAsync("commitment");
            var withdrawalZkey = await source.GetZkeyAsync("withdrawal");
            var commitmentVkey = source.GetVerificationKeyJson("commitment");
            var withdrawalVkey = source.GetVerificationKeyJson("withdrawal");

            Assert.NotEmpty(commitmentWasm);
            Assert.NotEmpty(withdrawalZkey);
            Assert.Contains("\"protocol\": \"groth16\"", commitmentVkey);
            Assert.Contains("\"protocol\": \"groth16\"", withdrawalVkey);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-ArtifactVerification")]
        public async Task MissingHash_ThrowsInvalidOperationException()
        {
            var testBytes = new byte[] { 10, 20, 30 };
            File.WriteAllBytes(Path.Combine(_cacheDir, "test.wasm"), testBytes);

            var source = new UrlCircuitArtifactSource("http://unused",
                cacheDir: _cacheDir,
                expectedHashes: new Dictionary<string, string>());

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => source.GetWasmAsync("test"));

            Assert.Contains("Refusing to load unverified artifact", ex.Message);
        }

        [Fact]
        [Trait("Category", "PrivacyPools-ArtifactVerification")]
        public void CircuitArtifactHashes_ContainsAllExpectedEntries()
        {
            var hashes = CircuitArtifactHashes.Default;

            Assert.Equal(10, hashes.Count);
            Assert.True(hashes.ContainsKey("commitment.wasm"));
            Assert.True(hashes.ContainsKey("commitment.zkey"));
            Assert.True(hashes.ContainsKey("commitment.vkey"));
            Assert.True(hashes.ContainsKey("commitment_vk.json"));
            Assert.True(hashes.ContainsKey("withdraw.wasm"));
            Assert.True(hashes.ContainsKey("withdraw.zkey"));
            Assert.True(hashes.ContainsKey("withdraw.vkey"));
            Assert.True(hashes.ContainsKey("withdrawal.wasm"));
            Assert.True(hashes.ContainsKey("withdrawal.zkey"));
            Assert.True(hashes.ContainsKey("withdrawal_vk.json"));
        }

        private static string NormalizeToLf(string text)
        {
            return text.Replace("\r\n", "\n");
        }
    }
}
