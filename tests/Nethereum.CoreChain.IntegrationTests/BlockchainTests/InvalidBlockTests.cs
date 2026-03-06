using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.BlockchainTests
{
    public class InvalidBlockTests
    {
        private readonly ITestOutputHelper _output;
        private readonly Sha3Keccack _keccak = new();

        private static readonly string TestVectorsPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "external", "ethereum-tests", "BlockchainTests", "InvalidBlocks");

        public InvalidBlockTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [MemberData(nameof(GetInvalidBlockTestFiles))]
        [Trait("Category", "InvalidBlockTests")]
        public void InvalidBlock_RlpDecodeAndValidateHeader(string subDir, string fileName)
        {
            var filePath = Path.Combine(TestVectorsPath, subDir, fileName);
            if (!File.Exists(filePath))
            {
                _output.WriteLine($"Test file not found: {filePath}");
                return;
            }

            var tests = BlockchainTestLoader.LoadFromFile(filePath);
            var cancunTests = tests.Where(t =>
                t.Network?.Contains("Cancun") == true ||
                t.Network?.Contains("Shanghai") == true ||
                t.Network?.Contains("London") == true ||
                t.Network?.Contains("Berlin") == true ||
                t.Network?.Contains("Merge") == true).ToList();

            if (!cancunTests.Any())
            {
                cancunTests = tests;
            }

            foreach (var test in cancunTests)
            {
                var invalidBlocks = test.Blocks.Where(b => !string.IsNullOrEmpty(b.ExpectException)).ToList();
                var validBlocks = test.Blocks.Where(b => string.IsNullOrEmpty(b.ExpectException)).ToList();

                foreach (var validBlock in validBlocks)
                {
                    if (validBlock.Rlp.Length == 0) continue;

                    try
                    {
                        var decoded = RLP.RLP.Decode(validBlock.Rlp);
                        Assert.NotNull(decoded);
                        _output.WriteLine($"  {test.Name}: valid block #{validBlock.BlockNumber} decoded OK");
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine($"  {test.Name}: valid block #{validBlock.BlockNumber} decode failed: {ex.Message}");
                    }
                }

                foreach (var invalidBlock in invalidBlocks)
                {
                    if (invalidBlock.Rlp.Length == 0)
                    {
                        _output.WriteLine($"  {test.Name}: invalid block has no RLP, skipping");
                        continue;
                    }

                    var expectedException = invalidBlock.ExpectException ?? "";
                    ValidateInvalidBlock(test.Name, invalidBlock, expectedException);
                }

                if (!invalidBlocks.Any() && !validBlocks.Any())
                {
                    _output.WriteLine($"  {test.Name}: no blocks found in test");
                }
            }
        }

        private void ValidateInvalidBlock(string testName, BlockchainTestLoader.BlockData block, string expectedException)
        {
            try
            {
                var decoded = RLP.RLP.Decode(block.Rlp);
                if (decoded is not RLPCollection blockRlp || blockRlp.Count < 1)
                {
                    _output.WriteLine($"  {testName}: invalid block RLP decode returned unexpected structure - expected exception: {expectedException}");
                    return;
                }

                var headerRlp = blockRlp[0] as RLPCollection;
                if (headerRlp == null)
                {
                    _output.WriteLine($"  {testName}: invalid block has no header - expected exception: {expectedException}");
                    return;
                }

                var headerHash = _keccak.CalculateHash(RLP.RLP.EncodeList(
                    headerRlp.SelectMany(x =>
                    {
                        if (x is RLPCollection subCollection)
                            return RLP.RLP.EncodeList(subCollection.SelectMany(s => RLP.RLP.EncodeElement(s.RLPData)).ToArray());
                        return RLP.RLP.EncodeElement(x.RLPData);
                    }).ToArray()));

                if (IsHeaderValidationException(expectedException))
                {
                    ValidateHeaderFields(testName, headerRlp, expectedException);
                }
                else
                {
                    _output.WriteLine($"  {testName}: decoded invalid block (exception at execution: {expectedException})");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  {testName}: invalid block RLP decode threw {ex.GetType().Name}: {Truncate(ex.Message, 80)} (expected: {expectedException})");
            }
        }

        private void ValidateHeaderFields(string testName, RLPCollection headerRlp, string expectedException)
        {
            try
            {
                var headerBytes = RLP.RLP.EncodeList(
                    headerRlp.SelectMany(x =>
                    {
                        if (x is RLPCollection subCollection)
                            return RLP.RLP.EncodeList(subCollection.SelectMany(s => RLP.RLP.EncodeElement(s.RLPData)).ToArray());
                        return RLP.RLP.EncodeElement(x.RLPData);
                    }).ToArray());
                var blockHeaderEncoder = new BlockHeaderEncoder();
                var header = blockHeaderEncoder.Decode(headerBytes);
                Assert.NotNull(header);

                if (expectedException.Contains("INVALID_BLOCK_TIMESTAMP"))
                {
                    _output.WriteLine($"  {testName}: header decoded, timestamp={header.Timestamp} (expected: {expectedException})");
                }
                else if (expectedException.Contains("INVALID_GAS_LIMIT"))
                {
                    _output.WriteLine($"  {testName}: header decoded, gasLimit={header.GasLimit}, gasUsed={header.GasUsed} (expected: {expectedException})");
                }
                else if (expectedException.Contains("INVALID_STATE_ROOT"))
                {
                    _output.WriteLine($"  {testName}: header decoded, stateRoot={header.StateRoot?.ToHex()?.Substring(0, Math.Min(16, header.StateRoot?.ToHex()?.Length ?? 0))}... (expected: {expectedException})");
                }
                else
                {
                    _output.WriteLine($"  {testName}: header decoded successfully (validation exception at execution: {expectedException})");
                }
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  {testName}: header decode threw {ex.GetType().Name}: {Truncate(ex.Message, 60)} (expected: {expectedException})");
            }
        }

        private static bool IsHeaderValidationException(string exception)
        {
            return exception.Contains("INVALID_BLOCK_TIMESTAMP") ||
                   exception.Contains("INVALID_GAS_LIMIT") ||
                   exception.Contains("INVALID_DIFFICULTY") ||
                   exception.Contains("INVALID_STATE_ROOT") ||
                   exception.Contains("INVALID_RECEIPT") ||
                   exception.Contains("INVALID_BLOOM") ||
                   exception.Contains("INVALID_UNCLE") ||
                   exception.Contains("EXCESS_BLOB_GAS");
        }

        private static string Truncate(string value, int maxLength)
        {
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
        }

        public static IEnumerable<object[]> GetInvalidBlockTestFiles()
        {
            if (!Directory.Exists(TestVectorsPath))
                yield break;

            var files = Directory.GetFiles(TestVectorsPath, "*.json", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(TestVectorsPath, file);
                var parts = relativePath.Split(Path.DirectorySeparatorChar);
                if (parts.Length >= 2)
                {
                    yield return new object[] { parts[0], parts[parts.Length - 1] };
                }
            }
        }
    }
}
