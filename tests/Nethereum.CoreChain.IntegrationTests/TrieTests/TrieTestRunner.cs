using System.Text;
using System.Text.Json;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.TrieTests
{
    public class TrieTestRunner
    {
        private readonly ITestOutputHelper _output;
        private readonly IHashProvider _hashProvider = new Sha3KeccackHashProvider();
        private readonly Sha3Keccack _keccak = new();

        private static readonly string TestPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "external", "ethereum-tests", "TrieTests");

        public TrieTestRunner(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [MemberData(nameof(GetTrieTests))]
        [Trait("Category", "TrieTests")]
        public void TrieRoot_MatchesExpected(string fileName, string testName, string expectedRoot)
        {
            var filePath = Path.Combine(TestPath, fileName);
            if (!File.Exists(filePath))
            {
                _output.WriteLine($"Test file not found: {filePath}");
                return;
            }

            var isSecure = fileName.Contains("secureTrie") || fileName.Contains("secure");
            var isAnyOrder = fileName.Contains("anyorder");
            var isHexEncoded = fileName.Contains("hex_encoded");

            using var doc = JsonDocument.Parse(File.ReadAllText(filePath));
            var testData = doc.RootElement.GetProperty(testName);
            var inputData = testData.GetProperty("in");

            var trie = new PatriciaTrie(_hashProvider);

            if (inputData.ValueKind == JsonValueKind.Array)
            {
                foreach (var pair in inputData.EnumerateArray())
                {
                    var keyStr = pair[0].GetString() ?? "";
                    var key = DecodeTestValue(keyStr, isHexEncoded);

                    if (isSecure)
                    {
                        key = _keccak.CalculateHash(key);
                    }

                    if (pair[1].ValueKind == JsonValueKind.Null)
                    {
                        trie.Delete(key);
                    }
                    else
                    {
                        var valueStr = pair[1].GetString() ?? "";
                        var value = DecodeTestValue(valueStr, isHexEncoded);
                        trie.Put(key, value);
                    }
                }
            }
            else if (inputData.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in inputData.EnumerateObject())
                {
                    var key = DecodeTestValue(prop.Name, isHexEncoded);

                    if (isSecure)
                    {
                        key = _keccak.CalculateHash(key);
                    }

                    var valueStr = prop.Value.GetString() ?? "";
                    var value = DecodeTestValue(valueStr, isHexEncoded);
                    trie.Put(key, value);
                }
            }

            var actualRoot = trie.Root.GetHash().ToHex(true);

            Assert.True(
                expectedRoot.IsTheSameHex(actualRoot),
                $"Root mismatch for '{testName}' in {fileName}.\n" +
                $"Expected: {expectedRoot}\n" +
                $"Actual:   {actualRoot}");

            _output.WriteLine($"  {fileName}/{testName}: root matches ({expectedRoot.Substring(0, 18)}...)");
        }

        private static byte[] DecodeTestValue(string value, bool isHexEncoded)
        {
            if (isHexEncoded) return value.HexToByteArray();
            if (value.StartsWith("0x") || value.StartsWith("0X")) return value.HexToByteArray();
            return Encoding.UTF8.GetBytes(value);
        }

        public static IEnumerable<object[]> GetTrieTests()
        {
            var testPath = Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "external", "ethereum-tests", "TrieTests");

            if (!Directory.Exists(testPath))
                yield break;

            var files = new[] { "trietest.json", "trietest_secureTrie.json", "trieanyorder.json",
                "trieanyorder_secureTrie.json", "hex_encoded_securetrie_test.json" };

            foreach (var fileName in files)
            {
                var filePath = Path.Combine(testPath, fileName);
                if (!File.Exists(filePath))
                    continue;

                using var doc = JsonDocument.Parse(File.ReadAllText(filePath));
                foreach (var testProp in doc.RootElement.EnumerateObject())
                {
                    var root = "";
                    if (testProp.Value.TryGetProperty("root", out var rootProp))
                    {
                        root = rootProp.GetString() ?? "";
                    }
                    yield return new object[] { fileName, testProp.Name, root };
                }
            }
        }
    }
}
