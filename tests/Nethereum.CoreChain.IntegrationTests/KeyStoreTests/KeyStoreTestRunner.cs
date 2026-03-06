using System.Text.Json;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.KeyStore;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.KeyStoreTests
{
    public class KeyStoreTestRunner
    {
        private readonly ITestOutputHelper _output;

        private static readonly string TestPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "external", "ethereum-tests", "KeyStoreTests");

        public KeyStoreTestRunner(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [MemberData(nameof(GetKeyStoreTests))]
        [Trait("Category", "KeyStoreTests")]
        public void KeyStore_DecryptRecoversPrivateKey(string testName, string password, string expectedPrivateKey, string keystoreJson)
        {
            var keyStoreService = new KeyStoreService();

            try
            {
                var decryptedKey = keyStoreService.DecryptKeyStoreFromJson(password, keystoreJson);
                var actualHex = decryptedKey.ToHex();

                Assert.True(
                    expectedPrivateKey.IsTheSameHex(actualHex),
                    $"Key mismatch for '{testName}'. Expected: {expectedPrivateKey}, Actual: {actualHex}");

                _output.WriteLine($"  {testName}: decrypted key matches expected ({actualHex.Substring(0, 16)}...)");
            }
            catch (Exception ex)
            {
                Assert.Fail($"KeyStore decryption failed for '{testName}': {ex.Message}");
            }
        }

        public static IEnumerable<object[]> GetKeyStoreTests()
        {
            var filePath = Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "external", "ethereum-tests", "KeyStoreTests", "basic_tests.json");

            if (!File.Exists(filePath))
                yield break;

            using var doc = JsonDocument.Parse(File.ReadAllText(filePath));
            foreach (var testProp in doc.RootElement.EnumerateObject())
            {
                var testName = testProp.Name;
                var testData = testProp.Value;

                if (!testData.TryGetProperty("password", out var passwordProp) ||
                    !testData.TryGetProperty("priv", out var privProp) ||
                    !testData.TryGetProperty("json", out var jsonProp))
                    continue;

                var password = passwordProp.GetString() ?? "";
                var priv = privProp.GetString() ?? "";
                var keystoreJson = jsonProp.GetRawText();

                yield return new object[] { testName, password, priv, keystoreJson };
            }
        }
    }
}
