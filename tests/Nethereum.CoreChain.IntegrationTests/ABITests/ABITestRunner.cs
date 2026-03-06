using System.Numerics;
using System.Text.Json;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.ABITests
{
    public class ABITestRunner
    {
        private readonly ITestOutputHelper _output;

        private static readonly string TestPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "external", "ethereum-tests", "ABITests");

        public ABITestRunner(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [MemberData(nameof(GetABITests))]
        [Trait("Category", "ABITests")]
        public void ABI_EncodeMatchesExpected(string testName, string[] types, string argsJson, string expectedResult)
        {
            var args = JsonDocument.Parse(argsJson).RootElement;

            try
            {
                var parameters = new List<Parameter>();
                var values = new List<object>();

                for (int i = 0; i < types.Length; i++)
                {
                    parameters.Add(new Parameter(types[i], i + 1));
                    values.Add(ConvertJsonToValue(args[i], types[i]));
                }

                var encoder = new ParametersEncoder();
                var encoded = encoder.EncodeParameters(parameters.ToArray(), values.ToArray());
                var actualHex = "0x" + encoded.ToHex();

                Assert.True(
                    expectedResult.IsTheSameHex(actualHex),
                    $"ABI encode mismatch for '{testName}'.\n" +
                    $"Expected: {expectedResult}\n" +
                    $"Actual:   {actualHex}");

                _output.WriteLine($"  {testName}: ABI encoding matches ({encoded.Length} bytes)");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  {testName}: ABI encoding failed: {ex.Message}");
                throw;
            }
        }

        private object ConvertJsonToValue(JsonElement element, string abiType)
        {
            if (abiType == "bool")
            {
                return element.GetBoolean();
            }

            if (abiType == "address")
            {
                return element.GetString() ?? "";
            }

            if (abiType == "string")
            {
                return element.GetString() ?? "";
            }

            if (abiType == "bytes")
            {
                var str = element.GetString() ?? "";
                if (str.StartsWith("0x") || str.StartsWith("0X"))
                    return str.HexToByteArray();
                return System.Text.Encoding.UTF8.GetBytes(str);
            }

            if (abiType.StartsWith("bytes") && !abiType.EndsWith("[]"))
            {
                var str = element.GetString() ?? "";
                if (str.StartsWith("0x") || str.StartsWith("0X"))
                    return str.HexToByteArray();
                return System.Text.Encoding.UTF8.GetBytes(str);
            }

            if (abiType.StartsWith("uint") || abiType.StartsWith("int"))
            {
                if (abiType.EndsWith("[]"))
                {
                    var list = new List<BigInteger>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(ParseBigInteger(item));
                    }
                    return list;
                }
                return ParseBigInteger(element);
            }

            if (abiType.EndsWith("[]"))
            {
                var baseType = abiType.Substring(0, abiType.Length - 2);
                var list = new List<object>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(ConvertJsonToValue(item, baseType));
                }
                return list;
            }

            return element.GetString() ?? "";
        }

        private BigInteger ParseBigInteger(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                if (element.TryGetInt64(out var longVal))
                    return new BigInteger(longVal);
                return BigInteger.Parse(element.GetRawText());
            }

            var str = element.GetString() ?? "0";
            if (str.StartsWith("0x") || str.StartsWith("0X"))
            {
                var hex = str.Substring(2);
                if (string.IsNullOrEmpty(hex)) return BigInteger.Zero;
                return BigInteger.Parse("0" + hex, System.Globalization.NumberStyles.HexNumber);
            }
            return BigInteger.Parse(str);
        }

        public static IEnumerable<object[]> GetABITests()
        {
            var filePath = Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "external", "ethereum-tests", "ABITests", "basic_abi_tests.json");

            if (!File.Exists(filePath))
                yield break;

            using var doc = JsonDocument.Parse(File.ReadAllText(filePath));
            foreach (var testProp in doc.RootElement.EnumerateObject())
            {
                var testName = testProp.Name;
                var testData = testProp.Value;

                if (!testData.TryGetProperty("types", out var typesProp) ||
                    !testData.TryGetProperty("args", out var argsProp) ||
                    !testData.TryGetProperty("result", out var resultProp))
                    continue;

                var types = new List<string>();
                foreach (var t in typesProp.EnumerateArray())
                {
                    types.Add(t.GetString() ?? "");
                }

                var expectedResult = resultProp.GetString() ?? "";
                var argsJson = argsProp.GetRawText();

                yield return new object[] { testName, types.ToArray(), argsJson, expectedResult };
            }
        }
    }
}
