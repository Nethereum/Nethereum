using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.Contracts.IntegrationTests.Patricia
{
    public static class GethTrieTestLoader
    {
        private static readonly string TestVectorsPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", "..",
            "external", "ethereum-tests", "TrieTests");

        public class TrieTestCase
        {
            public string Name { get; set; }
            public string FileName { get; set; }
            public bool IsSecureTrie { get; set; }
            public bool IsOrdered { get; set; }
            public bool IsHexEncoded { get; set; }
            public List<(byte[] Key, byte[] Value)> Operations { get; set; }
            public byte[] ExpectedRoot { get; set; }

            public override string ToString() => $"{FileName}/{Name}";
        }

        public static IEnumerable<object[]> GetAllTestCases()
        {
            foreach (var testCase in LoadAllTestCases())
            {
                yield return new object[] { testCase };
            }
        }

        public static IEnumerable<object[]> GetPlainTrieTestCases()
        {
            foreach (var testCase in LoadAllTestCases().Where(t => !t.IsSecureTrie))
            {
                yield return new object[] { testCase };
            }
        }

        public static IEnumerable<object[]> GetSecureTrieTestCases()
        {
            foreach (var testCase in LoadAllTestCases().Where(t => t.IsSecureTrie))
            {
                yield return new object[] { testCase };
            }
        }

        public static IEnumerable<TrieTestCase> LoadAllTestCases()
        {
            foreach (var testCase in LoadTrieTestFile("trietest.json", isSecure: false, isOrdered: true))
                yield return testCase;

            foreach (var testCase in LoadTrieAnyOrderFile("trieanyorder.json", isSecure: false))
                yield return testCase;

            foreach (var testCase in LoadTrieTestFile("trietest_secureTrie.json", isSecure: true, isOrdered: true))
                yield return testCase;

            foreach (var testCase in LoadTrieAnyOrderFile("trieanyorder_secureTrie.json", isSecure: true))
                yield return testCase;

            foreach (var testCase in LoadHexEncodedSecureTrieFile("hex_encoded_securetrie_test.json"))
                yield return testCase;
        }

        private static IEnumerable<TrieTestCase> LoadTrieTestFile(string fileName, bool isSecure, bool isOrdered)
        {
            var filePath = Path.Combine(TestVectorsPath, fileName);
            if (!File.Exists(filePath))
                yield break;

            var json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);

            foreach (var testProp in doc.RootElement.EnumerateObject())
            {
                var testName = testProp.Name;
                var testObj = testProp.Value;

                var operations = new List<(byte[] Key, byte[] Value)>();
                var inArray = testObj.GetProperty("in");

                foreach (var op in inArray.EnumerateArray())
                {
                    var keyStr = op[0].GetString();
                    var valueElement = op[1];

                    var keyBytes = ConvertKeyToBytes(keyStr);
                    byte[] valueBytes = null;

                    if (valueElement.ValueKind != JsonValueKind.Null)
                    {
                        valueBytes = ConvertValueToBytes(valueElement.GetString());
                    }

                    operations.Add((keyBytes, valueBytes));
                }

                var rootStr = testObj.GetProperty("root").GetString();
                var rootBytes = rootStr.Replace("0x", "").HexToByteArray();

                yield return new TrieTestCase
                {
                    Name = testName,
                    FileName = fileName,
                    IsSecureTrie = isSecure,
                    IsOrdered = isOrdered,
                    IsHexEncoded = false,
                    Operations = operations,
                    ExpectedRoot = rootBytes
                };
            }
        }

        private static IEnumerable<TrieTestCase> LoadTrieAnyOrderFile(string fileName, bool isSecure)
        {
            var filePath = Path.Combine(TestVectorsPath, fileName);
            if (!File.Exists(filePath))
                yield break;

            var json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);

            foreach (var testProp in doc.RootElement.EnumerateObject())
            {
                var testName = testProp.Name;
                var testObj = testProp.Value;

                var operations = new List<(byte[] Key, byte[] Value)>();
                var inObj = testObj.GetProperty("in");

                foreach (var kvp in inObj.EnumerateObject())
                {
                    var keyStr = kvp.Name;
                    var valueStr = kvp.Value.GetString();

                    var keyBytes = ConvertKeyToBytes(keyStr);
                    var valueBytes = ConvertValueToBytes(valueStr);

                    operations.Add((keyBytes, valueBytes));
                }

                var rootStr = testObj.GetProperty("root").GetString();
                var rootBytes = rootStr.Replace("0x", "").HexToByteArray();

                yield return new TrieTestCase
                {
                    Name = testName,
                    FileName = fileName,
                    IsSecureTrie = isSecure,
                    IsOrdered = false,
                    IsHexEncoded = false,
                    Operations = operations,
                    ExpectedRoot = rootBytes
                };
            }
        }

        private static IEnumerable<TrieTestCase> LoadHexEncodedSecureTrieFile(string fileName)
        {
            var filePath = Path.Combine(TestVectorsPath, fileName);
            if (!File.Exists(filePath))
                yield break;

            var json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);

            foreach (var testProp in doc.RootElement.EnumerateObject())
            {
                var testName = testProp.Name;
                var testObj = testProp.Value;

                var operations = new List<(byte[] Key, byte[] Value)>();
                var inObj = testObj.GetProperty("in");

                foreach (var kvp in inObj.EnumerateObject())
                {
                    var keyStr = kvp.Name;
                    var valueStr = kvp.Value.GetString();

                    var keyBytes = keyStr.Replace("0x", "").HexToByteArray();
                    var valueBytes = valueStr.Replace("0x", "").HexToByteArray();

                    operations.Add((keyBytes, valueBytes));
                }

                var rootStr = testObj.GetProperty("root").GetString();
                var rootBytes = rootStr.Replace("0x", "").HexToByteArray();

                yield return new TrieTestCase
                {
                    Name = testName,
                    FileName = fileName,
                    IsSecureTrie = true,
                    IsOrdered = false,
                    IsHexEncoded = true,
                    Operations = operations,
                    ExpectedRoot = rootBytes
                };
            }
        }

        private static byte[] ConvertKeyToBytes(string key)
        {
            if (key.StartsWith("0x") && key.Length > 2)
            {
                return key.Substring(2).HexToByteArray();
            }
            return Encoding.UTF8.GetBytes(key);
        }

        private static byte[] ConvertValueToBytes(string value)
        {
            if (value == null) return null;
            if (value.StartsWith("0x") && value.Length > 2)
            {
                return value.Substring(2).HexToByteArray();
            }
            return Encoding.UTF8.GetBytes(value);
        }

        public static byte[] HashKey(byte[] key)
        {
            return new Sha3Keccack().CalculateHash(key);
        }
    }
}
