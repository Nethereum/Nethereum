using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Ssz.Tests
{
    public class ProgressiveSpecVectorTests
    {
        private readonly ITestOutputHelper _output;
        private static readonly string VectorBasePath = FindVectorBasePath();

        public ProgressiveSpecVectorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static string FindVectorBasePath()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            for (var i = 0; i < 8; i++)
            {
                var candidate = Path.Combine(dir,
                    "tests", "LightClientVectors", "ssz", "consensus-spec-tests", "phase0", "ssz_generic");
                if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
                if (dir == null) break;
            }
            return null;
        }

        // --- ProgressiveBitlist: 700 vectors ---

        public static IEnumerable<object[]> ProgressiveBitlistCases()
        {
            var basePath = FindVectorBasePath();
            if (basePath == null) yield break;
            var dir = Path.Combine(basePath, "progressive_bitlist", "valid");
            if (!Directory.Exists(dir)) yield break;
            foreach (var caseDir in Directory.GetDirectories(dir).OrderBy(d => d))
            {
                yield return new object[] { Path.GetFileName(caseDir) };
            }
        }

        [Theory]
        [MemberData(nameof(ProgressiveBitlistCases))]
        [Trait("Category", "ConsensusSpecVector")]
        public void ProgressiveBitlist_AllVectors(string caseName)
        {
            var caseDir = Path.Combine(VectorBasePath, "progressive_bitlist", "valid", caseName);
            var expectedRoot = ParseRoot(caseDir);
            var serializedHex = ParseSingleHexValue(caseDir);
            var serialized = serializedHex.HexToByteArray();
            var bits = DeserializeBitlist(serialized);

            var result = SszMerkleizer.HashTreeRootProgressiveBitlist(bits);
            Assert.Equal(expectedRoot, result);
        }

        // --- BasicProgressiveList: 320 vectors ---

        public static IEnumerable<object[]> BasicProgressiveListCases()
        {
            var basePath = FindVectorBasePath();
            if (basePath == null) yield break;
            var dir = Path.Combine(basePath, "basic_progressive_list", "valid");
            if (!Directory.Exists(dir)) yield break;
            foreach (var caseDir in Directory.GetDirectories(dir).OrderBy(d => d))
            {
                yield return new object[] { Path.GetFileName(caseDir) };
            }
        }

        [Theory]
        [MemberData(nameof(BasicProgressiveListCases))]
        [Trait("Category", "ConsensusSpecVector")]
        public void BasicProgressiveList_AllVectors(string caseName)
        {
            var caseDir = Path.Combine(VectorBasePath, "basic_progressive_list", "valid", caseName);
            var expectedRoot = ParseRoot(caseDir);
            var valueYaml = File.ReadAllText(Path.Combine(caseDir, "value.yaml")).Trim();

            // Determine element type from case name: proglist_{type}_{variant}_{index}
            var elementType = caseName.Split('_')[1]; // bool, uint8, uint16, uint32, uint64, uint128, uint256
            var elements = ParseBasicList(valueYaml);

            var packedChunks = PackBasicElementsToChunks(elements, elementType);
            var result = SszMerkleizer.HashTreeRootBasicProgressiveList(
                packedChunks, (ulong)elements.Length);
            Assert.Equal(expectedRoot, result);
        }

        // --- Helpers ---

        private static byte[] ParseRoot(string caseDir)
        {
            var metaYaml = File.ReadAllText(Path.Combine(caseDir, "meta.yaml"));
            var match = Regex.Match(metaYaml, @"root:\s*'(0x[0-9a-fA-F]+)'");
            if (!match.Success) throw new Exception($"Cannot parse root from {caseDir}/meta.yaml");
            return match.Groups[1].Value.HexToByteArray();
        }

        private static string ParseSingleHexValue(string caseDir)
        {
            var valueYaml = File.ReadAllText(Path.Combine(caseDir, "value.yaml")).Trim();
            var match = Regex.Match(valueYaml, @"'(0x[0-9a-fA-F]+)'");
            if (!match.Success) throw new Exception($"Cannot parse hex value from {caseDir}/value.yaml: {valueYaml}");
            return match.Groups[1].Value;
        }

        private static string[] ParseBasicList(string yaml)
        {
            yaml = yaml.Trim();
            if (yaml == "[]") return Array.Empty<string>();

            yaml = yaml.Replace("\n", " ").Replace("\r", "");
            var match = Regex.Match(yaml, @"\[(.*)\]", RegexOptions.Singleline);
            if (!match.Success) throw new Exception($"Cannot parse list: {yaml}");
            var inner = match.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(inner)) return Array.Empty<string>();
            return inner.Split(',')
                .Select(s => s.Trim().Trim('\'', '"'))
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
        }

        private static IList<byte[]> PackBasicElementsToChunks(string[] elements, string elementType)
        {
            if (elements.Length == 0)
                return new List<byte[]>();

            int elementSize = elementType switch
            {
                "bool" => 1,
                "uint8" => 1,
                "uint16" => 2,
                "uint32" => 4,
                "uint64" => 8,
                "uint128" => 16,
                "uint256" => 32,
                _ => throw new Exception($"Unknown element type: {elementType}")
            };

            // Serialize all elements into one byte array
            var totalBytes = elements.Length * elementSize;
            var serialized = new byte[totalBytes];
            for (var i = 0; i < elements.Length; i++)
            {
                var offset = i * elementSize;
                WriteBasicValue(serialized, offset, elements[i], elementType);
            }

            // Chunkify into 32-byte chunks
            var chunkCount = (totalBytes + 31) / 32;
            var chunks = new List<byte[]>(chunkCount);
            for (var i = 0; i < chunkCount; i++)
            {
                var chunk = new byte[32];
                var start = i * 32;
                var len = Math.Min(32, totalBytes - start);
                Buffer.BlockCopy(serialized, start, chunk, 0, len);
                chunks.Add(chunk);
            }

            return chunks;
        }

        private static void WriteBasicValue(byte[] buffer, int offset, string value, string type)
        {
            switch (type)
            {
                case "bool":
                    buffer[offset] = value == "true" || value == "True" || value == "1" ? (byte)1 : (byte)0;
                    break;
                case "uint8":
                    buffer[offset] = byte.Parse(value);
                    break;
                case "uint16":
                    BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset), ushort.Parse(value));
                    break;
                case "uint32":
                    BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset), uint.Parse(value));
                    break;
                case "uint64":
                    BinaryPrimitives.WriteUInt64LittleEndian(buffer.AsSpan(offset), ulong.Parse(value));
                    break;
                case "uint128":
                case "uint256":
                    var bigVal = System.Numerics.BigInteger.Parse(value);
                    var bigBytes = bigVal.ToByteArray(); // little-endian, may have sign byte
                    var targetSize = type == "uint128" ? 16 : 32;
                    // Copy only the value bytes (exclude trailing sign byte if present)
                    var copyLen = bigBytes.Length;
                    if (copyLen > targetSize && bigBytes[copyLen - 1] == 0)
                        copyLen = targetSize;
                    Buffer.BlockCopy(bigBytes, 0, buffer, offset, Math.Min(copyLen, targetSize));
                    break;
            }
        }

        private static bool[] DeserializeBitlist(byte[] serialized)
        {
            if (serialized == null || serialized.Length == 0)
                throw new ArgumentException("Bitlist must have at least the delimiter byte.");
            var lastByte = serialized[serialized.Length - 1];
            if (lastByte == 0) throw new ArgumentException("Invalid bitlist: last byte is zero.");
            var delimiterBitPosition = 7;
            while ((lastByte & (1 << delimiterBitPosition)) == 0) delimiterBitPosition--;
            var totalBits = (serialized.Length - 1) * 8 + delimiterBitPosition;
            var bits = new bool[totalBits];
            for (var i = 0; i < totalBits; i++)
            {
                bits[i] = (serialized[i / 8] & (1 << (i % 8))) != 0;
            }
            return bits;
        }
    }
}
