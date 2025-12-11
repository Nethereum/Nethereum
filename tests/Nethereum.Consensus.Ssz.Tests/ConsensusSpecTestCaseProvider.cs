using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Snappier;
using System.IO.Compression;

namespace Nethereum.Consensus.Ssz.Tests
{
    internal static class ConsensusSpecTestCaseProvider
    {
        private const string BaseRelativePath = "tests/LightClientVectors/ssz/consensus-spec-tests/deneb/ssz_static";
        private const string CaseFolder = "ssz_random";

        public static IEnumerable<ConsensusSpecTestCase> Load(string containerName)
        {
            var root = RepositoryPath.Root;
            if (string.IsNullOrEmpty(root))
            {
                yield break;
            }

            var basePath = Combine(root, BaseRelativePath);
            var containerPath = Path.Combine(basePath, containerName, CaseFolder);
            if (!Directory.Exists(containerPath))
            {
                yield break;
            }

            var caseDirectories = Directory
                .EnumerateDirectories(containerPath, "case_*", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var caseDirectory in caseDirectories)
            {
                var caseName = GetRelativeCaseName(basePath, containerName, caseDirectory);
                var serializedPath = Path.Combine(caseDirectory, "serialized.ssz_snappy");
                var rootsPath = Path.Combine(caseDirectory, "roots.yaml");
                if (!File.Exists(serializedPath) || !File.Exists(rootsPath))
                {
                    continue;
                }

                var serialized = DecodeSnappy(serializedPath);
                var rootBytes = ParseRoot(rootsPath);
                yield return new ConsensusSpecTestCase(containerName, caseName, serialized, rootBytes);
            }
        }

        private static byte[] DecodeSnappy(string path)
        {
            var compressed = File.ReadAllBytes(path);

            // Attempt raw block decoding first; if it fails, try framed.
            try { return Snappy.DecompressToArray(compressed); }
            catch
            {
                using var input = new MemoryStream(compressed);
                using var snappy = new SnappyStream(input, CompressionMode.Decompress, leaveOpen: false);
                using var ms = new MemoryStream();
                snappy.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private static byte[] ParseRoot(string path)
        {
            var text = File.ReadAllText(path);
            var hexIndex = text.IndexOf("0x", StringComparison.OrdinalIgnoreCase);
            if (hexIndex < 0)
            {
                throw new InvalidDataException($"Unable to find root hex value in '{path}'.");
            }

            var hex = text.Substring(hexIndex).Trim().Trim('\'', '"');
            return HexToBytes(hex);
        }

        private static string GetRelativeCaseName(string basePath, string containerName, string caseDirectory)
        {
            var containerBase = Path.Combine(basePath, containerName);
            var relative = Path.GetRelativePath(containerBase, caseDirectory);
            return relative.Replace('\\', '/');
        }

        private static byte[] HexToBytes(string value)
        {
            var hex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
            if (hex.Length % 2 != 0)
            {
                throw new InvalidDataException($"Invalid hex value '{value}'.");
            }

            var buffer = new byte[hex.Length / 2];
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return buffer;
        }

        private static string Combine(string root, string relative)
        {
            var segments = relative.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            var current = root;
            foreach (var segment in segments)
            {
                current = Path.Combine(current, segment);
            }

            return current;
        }
    }
}
