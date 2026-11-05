using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Nethereum.CoreChain;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Testing;
using Xunit;

namespace Nethereum.EEST.ConformanceRunner
{
    [Trait("Requires", "Externals")]
    [Trait("Corpus", "ethereum-tests")]
    public class EthereumTestsDifficultyConformanceTests
    {
        private static string Root => Path.Combine(TestEnvironment.ExternalPath("EthereumTests"), "DifficultyTests");

        public static TheoryData<string> Files()
        {
            var data = new TheoryData<string>();
            var root = Root;
            if (!Directory.Exists(root))
            {
                data.Add(Corpora.MissingCacheSentinel);
                return data;
            }

            var files = Directory.GetFiles(root, "*.json", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
            if (files.Count == 0)
            {
                data.Add(Corpora.NoFixturesSentinel);
                return data;
            }

            foreach (var file in files) data.Add(file);
            return data;
        }

        [Theory]
        [MemberData(nameof(Files))]
        public void Given_EveryVectorInADifficultyFile_When_TheDifficultyIsCalculated_Then_ItEqualsTheFixture(string file)
        {
            if (file == Corpora.MissingCacheSentinel)
            {
                Assert.Fail($"ethereum/tests DifficultyTests not found at '{Root}'. Set ExternalPaths:EthereumTests and follow external/README.md.");
                return;
            }

            if (file == Corpora.NoFixturesSentinel)
            {
                Assert.Fail($"ethereum/tests DifficultyTests at '{Root}' contains zero *.json files - a category discovering 0 files must fail loudly.");
                return;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, file)));
            var mismatches = new List<string>();
            var checkedVectors = 0;

            foreach (var test in document.RootElement.EnumerateObject())
            foreach (var network in test.Value.EnumerateObject())
            {
                if (network.Name == "_info") continue;

                var fork = HardforkNames.Parse(network.Name);
                foreach (var vector in network.Value.EnumerateObject())
                {
                    var v = vector.Value;
                    var parentNumber = Hex(v, "currentBlockNumber") - 1;
                    var calculated = EthashDifficulty.Calculate(
                        fork,
                        (ulong)Hex(v, "currentTimestamp"),
                        (ulong)parentNumber,
                        (ulong)Hex(v, "parentTimestamp"),
                        Hex(v, "parentDifficulty"),
                        Hex(v, "parentUncles") != 0);

                    checkedVectors++;
                    var expected = Hex(v, "currentDifficulty");
                    if (calculated != expected)
                        mismatches.Add($"{vector.Name}: expected 0x{expected:x} calculated 0x{calculated:x}");
                }
            }

            Assert.True(checkedVectors > 0, $"{file} held no vectors");
            Assert.True(mismatches.Count == 0,
                $"{mismatches.Count}/{checkedVectors} vectors differ in {file}: {string.Join("; ", mismatches.Take(5))}");
        }

        private static BigInteger Hex(JsonElement element, string property) =>
            BigInteger.Parse("0" + element.GetProperty(property).GetString()!.RemoveHexPrefix(),
                System.Globalization.NumberStyles.HexNumber);
    }
}
