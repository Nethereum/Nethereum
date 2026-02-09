using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.BlockchainTests
{
    public class BlockchainFullExecutionTests
    {
        private readonly ITestOutputHelper _output;
        private readonly Sha3Keccack _keccak = new();
        private readonly IHashProvider _hashProvider = new Sha3KeccackHashProvider();

        private static readonly string TestVectorsPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "external", "ethereum-tests", "BlockchainTests", "ValidBlocks");

        public BlockchainFullExecutionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        [Trait("Category", "BlockchainTests-Full")]
        public void CanLoadSimpleTxTest()
        {
            var filePath = Path.Combine(TestVectorsPath, "bcValidBlockTest", "SimpleTx.json");
            if (!File.Exists(filePath))
            {
                _output.WriteLine($"Test file not found: {filePath}");
                return;
            }

            var tests = BlockchainTestLoader.LoadFromFile(filePath);

            Assert.NotEmpty(tests);
            var test = tests[0];

            _output.WriteLine($"Test: {test.Name}");
            _output.WriteLine($"Network: {test.Network}");
            _output.WriteLine($"ChainId: {test.ChainId}");
            _output.WriteLine($"Pre-state accounts: {test.Pre.Count}");
            _output.WriteLine($"Post-state accounts: {test.PostState.Count}");
            _output.WriteLine($"Blocks: {test.Blocks.Count}");

            Assert.True(test.Pre.Count > 0, "Should have pre-state");
            Assert.True(test.PostState.Count > 0, "Should have post-state");
            Assert.True(test.Blocks.Count > 0, "Should have blocks");
            Assert.True(test.GenesisBlockHeader.StateRoot.Length == 32, "Genesis should have state root");
        }

        [Fact]
        [Trait("Category", "BlockchainTests-Full")]
        public void GenesisStateRoot_SimpleTx_MatchesExpected()
        {
            var filePath = Path.Combine(TestVectorsPath, "bcValidBlockTest", "SimpleTx.json");
            if (!File.Exists(filePath))
            {
                _output.WriteLine($"Test file not found: {filePath}");
                return;
            }

            var tests = BlockchainTestLoader.LoadFromFile(filePath);
            var test = tests.First(t => t.Network == "Cancun");

            var calculatedRoot = CalculateStateRoot(test.Pre);
            var expectedRoot = test.GenesisBlockHeader.StateRoot;

            _output.WriteLine($"Expected state root: {expectedRoot.ToHex()}");
            _output.WriteLine($"Calculated state root: {calculatedRoot.ToHex()}");
            _output.WriteLine($"Pre-state accounts:");
            foreach (var acc in test.Pre)
            {
                _output.WriteLine($"  {acc.Key}: balance={acc.Value.Balance}, nonce={acc.Value.Nonce}, code={acc.Value.Code.Length} bytes, storage={acc.Value.Storage.Count} slots");
            }

            Assert.True(
                expectedRoot.ToHex().IsTheSameHex(calculatedRoot.ToHex()),
                $"Genesis state root mismatch.\nExpected: {expectedRoot.ToHex()}\nActual:   {calculatedRoot.ToHex()}");
        }

        [Theory]
        [MemberData(nameof(GetAllValidBlockTestFiles))]
        [Trait("Category", "BlockchainTests-Full")]
        public void GenesisStateRoot_MatchesExpected(string subDir, string fileName)
        {
            var filePath = Path.Combine(TestVectorsPath, subDir, fileName);
            if (!File.Exists(filePath))
            {
                _output.WriteLine($"Test file not found: {filePath}");
                return;
            }

            var tests = BlockchainTestLoader.LoadFromFile(filePath);

            foreach (var test in tests.Where(t => t.Network == "Cancun"))
            {
                _output.WriteLine($"Test: {test.Name}");

                var calculatedRoot = CalculateStateRoot(test.Pre);
                var expectedRoot = test.GenesisBlockHeader.StateRoot;

                _output.WriteLine($"  Expected: {expectedRoot.ToHex()}");
                _output.WriteLine($"  Actual:   {calculatedRoot.ToHex()}");
                _output.WriteLine($"  Accounts: {test.Pre.Count}");

                Assert.True(
                    expectedRoot.ToHex().IsTheSameHex(calculatedRoot.ToHex()),
                    $"[{test.Name}] Genesis state root mismatch");
            }
        }

        [Fact]
        [Trait("Category", "BlockchainTests-Full")]
        public void CountAvailableTestFiles()
        {
            if (!Directory.Exists(TestVectorsPath))
            {
                _output.WriteLine($"Test vectors directory not found: {TestVectorsPath}");
                return;
            }

            var files = BlockchainTestLoader.GetTestFilesInDirectory(TestVectorsPath).ToList();
            _output.WriteLine($"Found {files.Count} test files in ValidBlocks");

            var byCategory = files
                .GroupBy(f => Path.GetDirectoryName(f)?.Split(Path.DirectorySeparatorChar).Last() ?? "unknown")
                .OrderBy(g => g.Key);

            foreach (var group in byCategory)
            {
                _output.WriteLine($"  {group.Key}: {group.Count()} files");
            }
        }

        [Theory]
        [MemberData(nameof(GetAllValidBlockTestFiles))]
        [Trait("Category", "BlockchainTests-Full")]
        public void TransactionRoot_MatchesExpected(string subDir, string fileName)
        {
            var filePath = Path.Combine(TestVectorsPath, subDir, fileName);
            if (!File.Exists(filePath))
            {
                _output.WriteLine($"Test file not found: {filePath}");
                return;
            }

            var tests = BlockchainTestLoader.LoadFromFile(filePath);
            var rootCalculator = new RootCalculator();

            foreach (var test in tests.Where(t => t.Network == "Cancun"))
            {
                foreach (var block in test.Blocks)
                {
                    _output.WriteLine($"Test: {test.Name}, Block: {block.BlockNumber}");
                    _output.WriteLine($"  Transactions: {block.Transactions.Count}");

                    var encodedTxs = ExtractTransactionsFromBlockRlp(block.Rlp);
                    Assert.NotNull(encodedTxs);
                    Assert.Equal(block.Transactions.Count, encodedTxs.Count);

                    foreach (var encoded in encodedTxs)
                    {
                        _output.WriteLine($"  TX RLP: {encoded.ToHex().Substring(0, Math.Min(80, encoded.ToHex().Length))}...");
                    }

                    var actualRoot = rootCalculator.CalculateTransactionsRoot(encodedTxs);
                    var expectedRoot = block.BlockHeader.TransactionsRoot;

                    _output.WriteLine($"  Expected TX root: {expectedRoot.ToHex()}");
                    _output.WriteLine($"  Actual TX root:   {actualRoot.ToHex()}");

                    Assert.True(
                        expectedRoot.ToHex().IsTheSameHex(actualRoot.ToHex()),
                        $"[{test.Name}] Transaction root mismatch for block {block.BlockNumber}");
                }
            }
        }

        private List<byte[]>? ExtractTransactionsFromBlockRlp(byte[] blockRlp)
        {
            if (blockRlp == null || blockRlp.Length == 0)
                return null;

            try
            {
                var decoded = RLP.RLP.Decode(blockRlp) as RLPCollection;
                if (decoded == null || decoded.Count < 2)
                    return null;

                var txList = decoded[1] as RLPCollection;
                if (txList == null)
                    return null;

                var transactions = new List<byte[]>();
                foreach (var txElement in txList)
                {
                    if (txElement.RLPData != null)
                    {
                        transactions.Add(txElement.RLPData);
                    }
                }

                return transactions;
            }
            catch
            {
                return null;
            }
        }

        private byte[] CalculateStateRoot(Dictionary<string, BlockchainTestLoader.AccountData> accounts)
        {
            if (accounts.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            var trie = new PatriciaTrie(_hashProvider);

            foreach (var kvp in accounts)
            {
                var address = kvp.Key.HexToByteArray();
                var accountData = kvp.Value;

                var storageRoot = CalculateStorageRoot(accountData.Storage);

                var codeHash = accountData.Code.Length > 0
                    ? _keccak.CalculateHash(accountData.Code)
                    : DefaultValues.EMPTY_DATA_HASH;

                var account = new Account
                {
                    Nonce = accountData.Nonce,
                    Balance = accountData.Balance,
                    StateRoot = storageRoot,
                    CodeHash = codeHash
                };

                var hashedAddress = _keccak.CalculateHash(address);
                var encodedAccount = AccountEncoder.Current.Encode(account);

                trie.Put(hashedAddress, encodedAccount);
            }

            return trie.Root.GetHash();
        }

        private byte[] CalculateStorageRoot(Dictionary<BigInteger, BigInteger> storage)
        {
            if (storage.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            var trie = new PatriciaTrie(_hashProvider);

            foreach (var kvp in storage)
            {
                var slot = kvp.Key.ToBytesForRLPEncoding();
                if (slot.Length < 32)
                {
                    var paddedSlot = new byte[32];
                    Array.Copy(slot, 0, paddedSlot, 32 - slot.Length, slot.Length);
                    slot = paddedSlot;
                }

                var hashedSlot = _keccak.CalculateHash(slot);

                var value = kvp.Value.ToBytesForRLPEncoding();
                var encodedValue = RLP.RLP.EncodeElement(value);

                trie.Put(hashedSlot, encodedValue);
            }

            return trie.Root.GetHash();
        }

        private LegacyTransaction CreateLegacyTransaction(BlockchainTestLoader.TransactionData tx)
        {
            return new LegacyTransaction(
                nonce: tx.Nonce.ToBytesForRLPEncoding(),
                gasPrice: tx.GasPrice.ToBytesForRLPEncoding(),
                gasLimit: tx.GasLimit.ToBytesForRLPEncoding(),
                receiveAddress: tx.To?.HexToByteArray() ?? Array.Empty<byte>(),
                value: tx.Value.ToBytesForRLPEncoding(),
                data: tx.Data,
                r: tx.R,
                s: tx.S,
                v: (byte)tx.V);
        }

        public static IEnumerable<object[]> GetAllValidBlockTestFiles()
        {
            var testVectorsPath = Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "external", "ethereum-tests", "BlockchainTests", "ValidBlocks");

            if (!Directory.Exists(testVectorsPath))
                yield break;

            var files = Directory.GetFiles(testVectorsPath, "*.json", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(testVectorsPath, file);
                var parts = relativePath.Split(Path.DirectorySeparatorChar);
                if (parts.Length >= 2)
                {
                    var subDir = parts[0];
                    var fileName = parts[parts.Length - 1];
                    yield return new object[] { subDir, fileName };
                }
            }
        }
    }
}
