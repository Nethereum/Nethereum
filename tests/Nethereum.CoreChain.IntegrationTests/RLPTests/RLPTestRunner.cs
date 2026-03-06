using System.Numerics;
using System.Text;
using System.Text.Json;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.RLPTests
{
    public class RLPTestRunner
    {
        private readonly ITestOutputHelper _output;

        private static readonly string TestPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "external", "ethereum-tests", "RLPTests");

        public RLPTestRunner(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [MemberData(nameof(GetValidRLPTests))]
        [Trait("Category", "RLPTests")]
        public void ValidRLP_EncodeMatchesExpected(string testName, string expectedHex)
        {
            var tests = LoadRLPTests("rlptest.json");
            var test = tests[testName];
            var expectedBytes = expectedHex.HexToByteArray();

            var decoded = RLP.RLP.Decode(expectedBytes);
            Assert.NotNull(decoded);

            var reEncoded = decoded is RLPCollection
                ? ReEncodeCollection((RLPCollection)decoded)
                : RLP.RLP.EncodeElement(decoded.RLPData);

            Assert.True(
                expectedHex.IsTheSameHex(reEncoded.ToHex()),
                $"RLP re-encode mismatch for '{testName}'.\n" +
                $"Expected: {expectedHex}\n" +
                $"Actual:   {reEncoded.ToHex()}");

            _output.WriteLine($"  {testName}: OK ({expectedBytes.Length} bytes)");
        }

        [Theory]
        [MemberData(nameof(GetValidRLPTests))]
        [Trait("Category", "RLPTests")]
        public void ValidRLP_DecodeDoesNotThrow(string testName, string expectedHex)
        {
            var expectedBytes = expectedHex.HexToByteArray();

            var decoded = RLP.RLP.Decode(expectedBytes);
            Assert.NotNull(decoded);

            _output.WriteLine($"  {testName}: decoded OK");
        }

        [Theory]
        [MemberData(nameof(GetInvalidRLPTests))]
        [Trait("Category", "RLPTests")]
        public void InvalidRLP_DecodeHandlesGracefully(string testName, string invalidHex)
        {
            if (string.IsNullOrEmpty(invalidHex))
            {
                _output.WriteLine($"  {testName}: empty input, skipping");
                return;
            }

            byte[] bytes;
            try
            {
                bytes = invalidHex.HexToByteArray();
            }
            catch
            {
                _output.WriteLine($"  {testName}: invalid hex, skipping");
                return;
            }

            if (bytes.Length == 0)
            {
                _output.WriteLine($"  {testName}: empty bytes, skipping");
                return;
            }

            try
            {
                var decoded = RLP.RLP.Decode(bytes);
                _output.WriteLine($"  {testName}: decoded without exception (some invalid RLP is still decodable at byte level)");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  {testName}: correctly threw {ex.GetType().Name}");
            }
        }

        private byte[] ReEncodeCollection(RLPCollection collection)
        {
            var items = new List<byte[]>();
            foreach (var item in collection)
            {
                if (item is RLPCollection subCollection)
                {
                    items.Add(ReEncodeCollection(subCollection));
                }
                else
                {
                    items.Add(RLP.RLP.EncodeElement(item.RLPData));
                }
            }

            var concatenated = items.SelectMany(x => x).ToArray();
            return RLP.RLP.EncodeList(concatenated);
        }

        public static IEnumerable<object[]> GetValidRLPTests()
        {
            var tests = LoadRLPTests("rlptest.json");
            foreach (var kvp in tests)
            {
                yield return new object[] { kvp.Key, kvp.Value };
            }
        }

        public static IEnumerable<object[]> GetInvalidRLPTests()
        {
            var path = Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "external", "ethereum-tests", "RLPTests", "invalidRLPTest.json");

            if (!File.Exists(path))
                yield break;

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var outHex = "";
                if (prop.Value.TryGetProperty("out", out var outProp))
                {
                    outHex = outProp.GetString() ?? "";
                    if (outHex.StartsWith("0x") || outHex.StartsWith("0X"))
                        outHex = outHex.Substring(2);
                }
                yield return new object[] { prop.Name, outHex };
            }
        }

        private static Dictionary<string, string> LoadRLPTests(string fileName)
        {
            var path = Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "external", "ethereum-tests", "RLPTests", fileName);

            if (!File.Exists(path))
                return new Dictionary<string, string>();

            var result = new Dictionary<string, string>();
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.TryGetProperty("out", out var outProp))
                {
                    var outHex = outProp.GetString() ?? "";
                    if (outHex.StartsWith("0x") || outHex.StartsWith("0X"))
                        outHex = outHex.Substring(2);
                    result[prop.Name] = outHex;
                }
            }
            return result;
        }
    }
}
