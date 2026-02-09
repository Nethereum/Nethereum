using System.Numerics;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.BlockchainTests
{
    public class BlockchainTestExecutionTests
    {
        private readonly ITestOutputHelper _output;
        private readonly Sha3Keccack _keccak = new();

        private static readonly string TestVectorsPath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "external", "ethereum-tests", "BlockchainTests", "ValidBlocks");

        public BlockchainTestExecutionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        [Trait("Category", "BlockchainTests-E2E")]
        public async Task SimpleTx_ExecutesAndMatchesExpectedState()
        {
            var filePath = Path.Combine(TestVectorsPath, "bcValidBlockTest", "SimpleTx.json");
            if (!File.Exists(filePath))
            {
                _output.WriteLine($"Test file not found: {filePath}");
                return;
            }

            var tests = BlockchainTestLoader.LoadFromFile(filePath);
            var test = tests.FirstOrDefault(t => t.Network == "Cancun");
            if (test == null)
            {
                _output.WriteLine("No Cancun test found in SimpleTx.json");
                return;
            }

            await ExecuteAndValidateTestAsync(test);
        }

        [Theory]
        [MemberData(nameof(GetSimpleBlockTestFiles))]
        [Trait("Category", "BlockchainTests-E2E")]
        public async Task ExecuteBlockchainTest(string subDir, string fileName)
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
                _output.WriteLine($"Executing test: {test.Name}");
                await ExecuteAndValidateTestAsync(test);
            }
        }

        private async Task ExecuteAndValidateTestAsync(BlockchainTestLoader.BlockchainTest test)
        {
            var config = new DevChainConfig
            {
                ChainId = test.ChainId,
                BlockGasLimit = (BigInteger)test.GenesisBlockHeader.GasLimit,
                AutoMine = false,
                Coinbase = test.GenesisBlockHeader.Coinbase.ToHex(true),
                BaseFee = test.GenesisBlockHeader.BaseFee ?? 0,
                Hardfork = test.Network.ToLowerInvariant()
            };

            using var node = new DevChainNode(config);

            await SetupPreStateAsync(node, test.Pre);

            // DEBUG: Check balance after setup
            var debugAddr = "0xb1607e5700000000000000000000000000000000";
            var debugBalanceAfterSetup = await node.GetBalanceAsync(debugAddr);
            _output.WriteLine($"[DEBUG] Balance of {debugAddr} after setup: {debugBalanceAfterSetup}");

            await node.StartAsync();

            var genesisBlock = await node.GetBlockByNumberAsync(0);
            Assert.NotNull(genesisBlock);

            _output.WriteLine($"Genesis state root - Expected: {test.GenesisBlockHeader.StateRoot.ToHex()}");
            _output.WriteLine($"Genesis state root - Actual:   {genesisBlock.StateRoot.ToHex()}");

            Assert.True(
                test.GenesisBlockHeader.StateRoot.ToHex().IsTheSameHex(genesisBlock.StateRoot.ToHex()),
                $"Genesis state root mismatch");

            // Set the expected genesis block hash for BLOCKHASH(0) to work correctly
            if (test.GenesisBlockHeader.Hash != null && test.GenesisBlockHeader.Hash.Length == 32)
            {
                await node.SetBlockHashAsync(0, test.GenesisBlockHeader.Hash);
                _output.WriteLine($"Genesis block hash set to: {test.GenesisBlockHeader.Hash.ToHex()}");
            }

            // DEBUG: Check balance after genesis
            var debugBalanceAfterGenesis = await node.GetBalanceAsync(debugAddr);
            _output.WriteLine($"[DEBUG] Balance of {debugAddr} after genesis: {debugBalanceAfterGenesis}");

            foreach (var blockData in test.Blocks)
            {
                _output.WriteLine($"\n--- Executing Block {blockData.BlockNumber} ---");
                _output.WriteLine($"Transactions: {blockData.Transactions.Count}");

                var transactions = ExtractTransactionsFromBlockRlp(blockData.Rlp);
                var parentBeaconRoot = blockData.BlockHeader.ParentBeaconBlockRoot;
                var blockTimestamp = (long)blockData.BlockHeader.Timestamp;
                var blockBaseFee = blockData.BlockHeader.BaseFee ?? test.GenesisBlockHeader.BaseFee ?? 0;
                _output.WriteLine($"  ParentBeaconBlockRoot: {(parentBeaconRoot != null ? parentBeaconRoot.ToHex() : "null")} ({parentBeaconRoot?.Length ?? 0} bytes)");
                _output.WriteLine($"  Block timestamp from header: {blockTimestamp}");
                _output.WriteLine($"  Block baseFee from header: {blockBaseFee}");

                // Set the next block parameters to match the test vector
                node.DevConfig.NextBlockTimestamp = blockTimestamp;
                node.DevConfig.NextBlockBaseFee = blockBaseFee;
                node.DevConfig.NextBlockPrevRandao = blockData.BlockHeader.MixHash;
                node.DevConfig.NextBlockCoinbase = blockData.BlockHeader.Coinbase.ToHex(true);

                if (transactions == null || transactions.Count == 0)
                {
                    _output.WriteLine("No transactions to execute in this block");
                    await node.MineBlockAsync(parentBeaconRoot);
                    continue;
                }

                foreach (var txRlp in transactions)
                {
                    var signedTx = TransactionFactory.CreateTransaction(txRlp);
                    _output.WriteLine($"  Submitting TX: {signedTx.Hash.ToHex()}");

                    // For blockchain tests, bypass validation and add directly to pending
                    // This is necessary because multiple TXs from the same sender in a block
                    // have sequential nonces that can't be validated individually
                    node.BlockManager.AddPendingTransaction(signedTx);
                    _output.WriteLine($"  TX added to pending");
                }

                // DEBUG: Check balance before mining
                var debugBalanceBeforeMine = await node.GetBalanceAsync(debugAddr);
                _output.WriteLine($"[DEBUG] Balance of {debugAddr} before mining: {debugBalanceBeforeMine}");

                await node.MineBlockAsync(parentBeaconRoot);

                // DEBUG: Check balance after mining
                var debugBalanceAfterMine = await node.GetBalanceAsync(debugAddr);
                _output.WriteLine($"[DEBUG] Balance of {debugAddr} after mining: {debugBalanceAfterMine}");

                var latestBlock = await node.GetLatestBlockAsync();
                Assert.NotNull(latestBlock);

                _output.WriteLine($"\nBlock {blockData.BlockNumber} validation:");

                _output.WriteLine($"  TX Root - Expected: {blockData.BlockHeader.TransactionsRoot.ToHex()}");
                _output.WriteLine($"  TX Root - Actual:   {latestBlock.TransactionsHash.ToHex()}");
                Assert.True(
                    blockData.BlockHeader.TransactionsRoot.ToHex().IsTheSameHex(latestBlock.TransactionsHash.ToHex()),
                    $"Transaction root mismatch for block {blockData.BlockNumber}");

                _output.WriteLine($"  Receipt Root - Expected: {blockData.BlockHeader.ReceiptsRoot.ToHex()}");
                _output.WriteLine($"  Receipt Root - Actual:   {latestBlock.ReceiptHash.ToHex()}");

                // Show state root early (before receipt assertion) for diagnostics
                _output.WriteLine($"  State Root - Expected: {blockData.BlockHeader.StateRoot.ToHex()}");
                _output.WriteLine($"  State Root - Actual:   {latestBlock.StateRoot.ToHex()}");
                var stateRootMatches = blockData.BlockHeader.StateRoot.ToHex().IsTheSameHex(latestBlock.StateRoot.ToHex());
                _output.WriteLine($"  State Root Match: {stateRootMatches}");

                // Debug: Show receipt details when there's a mismatch
                if (!blockData.BlockHeader.ReceiptsRoot.ToHex().IsTheSameHex(latestBlock.ReceiptHash.ToHex()))
                {
                    _output.WriteLine($"  --- Receipt Details for block {blockData.BlockNumber} ---");
                    foreach (var txRlp in transactions ?? new List<byte[]>())
                    {
                        var signedTx = TransactionFactory.CreateTransaction(txRlp);
                        var txType = TransactionProcessor.GetTransactionType(signedTx);
                        var receiptInfo = await node.GetTransactionReceiptInfoAsync(signedTx.Hash);
                        if (receiptInfo != null)
                        {
                            _output.WriteLine($"    TX {signedTx.Hash.ToHex().Substring(0, 10)}... Type={txType}:");
                            _output.WriteLine($"      Status: {(receiptInfo.Receipt.HasSucceeded == true ? 1 : 0)}");
                            _output.WriteLine($"      CumulativeGasUsed: {receiptInfo.Receipt.CumulativeGasUsed}");
                            _output.WriteLine($"      GasUsed: {receiptInfo.GasUsed}");
                            _output.WriteLine($"      Logs: {receiptInfo.Receipt.Logs?.Count ?? 0}");
                            _output.WriteLine($"      Bloom: {receiptInfo.Receipt.Bloom?.ToHex().Substring(0, 20)}...");

                            // Show the encoded receipt
                            var encodedReceipt = txType > 0
                                ? ReceiptEncoder.Current.EncodeTyped(receiptInfo.Receipt, txType)
                                : ReceiptEncoder.Current.Encode(receiptInfo.Receipt);
                            _output.WriteLine($"      EncodedReceipt ({encodedReceipt.Length} bytes): {encodedReceipt.ToHex().Substring(0, Math.Min(100, encodedReceipt.Length * 2))}...");
                        }
                    }
                }

                // Temporarily continue past receipt mismatch to see state differences
                var receiptMatches = blockData.BlockHeader.ReceiptsRoot.ToHex().IsTheSameHex(latestBlock.ReceiptHash.ToHex());
                if (!receiptMatches)
                {
                    _output.WriteLine($"  WARNING: Receipt root mismatch (continuing to check state)");
                }

                _output.WriteLine($"  State Root - Expected: {blockData.BlockHeader.StateRoot.ToHex()}");
                _output.WriteLine($"  State Root - Actual:   {latestBlock.StateRoot.ToHex()}");

                if (!blockData.BlockHeader.StateRoot.ToHex().IsTheSameHex(latestBlock.StateRoot.ToHex()))
                {
                    _output.WriteLine($"  NOTE: State root mismatch indicates EVM execution difference vs Geth");

                    // Debug: Show actual state vs expected post-state
                    _output.WriteLine($"\n  --- Debug: Comparing actual vs expected post-state ---");
                    foreach (var postAcc in test.PostState)
                    {
                        var address = postAcc.Key;
                        var expected = postAcc.Value;
                        var actualBalance = await node.GetBalanceAsync(address);
                        var actualNonce = await node.GetNonceAsync(address);
                        var actualCode = await node.GetCodeAsync(address);

                        _output.WriteLine($"  {address}:");
                        _output.WriteLine($"    Balance: expected={expected.Balance}, actual={actualBalance}, match={expected.Balance == actualBalance}");
                        _output.WriteLine($"    Nonce:   expected={expected.Nonce}, actual={actualNonce}, match={expected.Nonce == actualNonce}");
                        _output.WriteLine($"    Code:    expected={expected.Code.Length} bytes, actual={actualCode?.Length ?? 0} bytes");

                        foreach (var storage in expected.Storage)
                        {
                            var actualValue = await node.GetStorageAtAsync(address, storage.Key);
                            var actualValueBigInt = actualValue?.ToBigIntegerFromRLPDecoded() ?? BigInteger.Zero;
                            _output.WriteLine($"    Storage[{storage.Key}]: expected={storage.Value}, actual={actualValueBigInt}, match={storage.Value == actualValueBigInt}");
                        }
                    }
                }

                // Fail at end after showing all comparisons
                if (!receiptMatches || !blockData.BlockHeader.StateRoot.ToHex().IsTheSameHex(latestBlock.StateRoot.ToHex()))
                {
                    _output.WriteLine($"\n  FINAL ASSERTION: Receipt match={receiptMatches}, State match={stateRootMatches}");
                    Assert.Fail($"Block {blockData.BlockNumber} - Receipt match={receiptMatches}, State match={stateRootMatches}");
                }
            }

            if (test.PostState.Count > 0)
            {
                _output.WriteLine("\n--- Validating Post-State ---");
                await ValidatePostStateAsync(node, test.PostState);
            }

            if (test.LastBlockHash.Length > 0)
            {
                var finalBlock = await node.GetLatestBlockAsync();
                var encodedHeader = BlockHeaderEncoder.Current.Encode(finalBlock);
                var finalBlockHash = _keccak.CalculateHash(encodedHeader);

                _output.WriteLine($"\nFinal Block Hash - Expected: {test.LastBlockHash.ToHex()}");
                _output.WriteLine($"Final Block Hash - Actual:   {finalBlockHash.ToHex()}");
            }

            _output.WriteLine("\n=== Test PASSED ===");
        }

        private async Task SetupPreStateAsync(DevChainNode node, Dictionary<string, BlockchainTestLoader.AccountData> preState)
        {
            foreach (var kvp in preState)
            {
                var address = kvp.Key;
                var account = kvp.Value;

                // Always set balance (even 0) to ensure the account is created in the state
                await node.SetBalanceAsync(address, account.Balance);

                // Always set nonce (even 0) for accounts that exist
                await node.SetNonceAsync(address, account.Nonce);

                if (account.Code.Length > 0)
                {
                    await node.SetCodeAsync(address, account.Code);
                }

                foreach (var storage in account.Storage)
                {
                    var slot = storage.Key;
                    var value = storage.Value.ToBytesForRLPEncoding();
                    await node.SetStorageAtAsync(address, slot, value);
                }
            }
        }

        private async Task ValidatePostStateAsync(DevChainNode node, Dictionary<string, BlockchainTestLoader.AccountData> postState)
        {
            foreach (var kvp in postState)
            {
                var address = kvp.Key;
                var expectedAccount = kvp.Value;

                var actualBalance = await node.GetBalanceAsync(address);
                if (actualBalance != expectedAccount.Balance)
                {
                    _output.WriteLine($"  BALANCE MISMATCH {address}: expected={expectedAccount.Balance}, actual={actualBalance}");
                }
                Assert.Equal(expectedAccount.Balance, actualBalance);

                var actualNonce = await node.GetNonceAsync(address);
                if (actualNonce != expectedAccount.Nonce)
                {
                    _output.WriteLine($"  NONCE MISMATCH {address}: expected={expectedAccount.Nonce}, actual={actualNonce}");
                }
                Assert.Equal(expectedAccount.Nonce, actualNonce);

                var actualCode = await node.GetCodeAsync(address);
                var expectedCodeHex = expectedAccount.Code.ToHex();
                var actualCodeHex = actualCode?.ToHex() ?? "";
                if (!expectedCodeHex.IsTheSameHex(actualCodeHex))
                {
                    _output.WriteLine($"  CODE MISMATCH {address}: expected={expectedCodeHex.Length} bytes, actual={actualCodeHex.Length} bytes");
                }
                Assert.True(expectedCodeHex.IsTheSameHex(actualCodeHex), $"Code mismatch for {address}");

                foreach (var storage in expectedAccount.Storage)
                {
                    var slot = storage.Key;
                    var expectedValue = storage.Value;
                    var actualValue = await node.GetStorageAtAsync(address, slot);
                    var actualValueBigInt = actualValue?.ToBigIntegerFromRLPDecoded() ?? BigInteger.Zero;

                    if (actualValueBigInt != expectedValue)
                    {
                        _output.WriteLine($"  STORAGE MISMATCH {address}[{slot}]: expected={expectedValue}, actual={actualValueBigInt}");
                    }
                    Assert.Equal(expectedValue, actualValueBigInt);
                }

                _output.WriteLine($"  {address}: OK (balance={actualBalance}, nonce={actualNonce}, code={actualCode?.Length ?? 0} bytes, storage={expectedAccount.Storage.Count} slots)");
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

        public static IEnumerable<object[]> GetSimpleBlockTestFiles()
        {
            var testVectorsPath = Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "external", "ethereum-tests", "BlockchainTests", "ValidBlocks");

            if (!Directory.Exists(testVectorsPath))
                yield break;

            var simpleTests = new[]
            {
                ("bcValidBlockTest", "SimpleTx.json"),
                ("bcValidBlockTest", "EmptyBlock.json"),
                ("bcValidBlockTest", "gasUsage.json"),
            };

            foreach (var (subDir, fileName) in simpleTests)
            {
                var filePath = Path.Combine(testVectorsPath, subDir, fileName);
                if (File.Exists(filePath))
                {
                    yield return new object[] { subDir, fileName };
                }
            }
        }

        [Fact]
        [Trait("Category", "BlockchainTests-SelfDestructBalance")]
        public async Task SelfDestructBalance_Cancun()
        {
            var filePath = Path.Combine(TestVectorsPath, "bcStateTests", "selfdestructBalance.json");
            if (!File.Exists(filePath))
            {
                _output.WriteLine($"Test file not found: {filePath}");
                return;
            }

            var tests = BlockchainTestLoader.LoadFromFile(filePath);
            var test = tests.FirstOrDefault(t => t.Network == "Cancun");
            if (test == null)
            {
                _output.WriteLine("No Cancun test found in selfdestructBalance.json");
                return;
            }

            _output.WriteLine($"Test: {test.Name}");
            _output.WriteLine($"Expected gasUsed (block): {test.Blocks[0].BlockHeader.GasUsed}");

            await ExecuteAndValidateTestAsync(test);
        }

        [Theory]
        [MemberData(nameof(GetAllValidBlockTestFiles))]
        [Trait("Category", "BlockchainTests-E2E-Full")]
        public async Task ExecuteAllBlockchainTests(string subDir, string fileName)
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
                _output.WriteLine($"\n========================================");
                _output.WriteLine($"Test: {test.Name}");
                _output.WriteLine($"File: {subDir}/{fileName}");
                _output.WriteLine($"========================================");

                try
                {
                    await ExecuteAndValidateTestAsync(test);
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"FAILED: {ex.Message}");
                    throw;
                }
            }
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

        [Fact]
        [Trait("Category", "BlockchainTests-E2E")]
        public async Task ManualTest_SimpleTxWithDetailedOutput()
        {
            var filePath = Path.Combine(TestVectorsPath, "bcValidBlockTest", "SimpleTx.json");
            if (!File.Exists(filePath))
            {
                _output.WriteLine($"Test file not found: {filePath}");
                return;
            }

            var tests = BlockchainTestLoader.LoadFromFile(filePath);
            var test = tests.FirstOrDefault(t => t.Network == "Cancun");
            if (test == null)
            {
                _output.WriteLine("No Cancun test found");
                return;
            }

            _output.WriteLine($"Test: {test.Name}");
            _output.WriteLine($"Network: {test.Network}");
            _output.WriteLine($"ChainId: {test.ChainId}");
            _output.WriteLine($"\n--- Pre-State ({test.Pre.Count} accounts) ---");
            foreach (var acc in test.Pre)
            {
                _output.WriteLine($"  {acc.Key}:");
                _output.WriteLine($"    balance: {acc.Value.Balance}");
                _output.WriteLine($"    nonce: {acc.Value.Nonce}");
                _output.WriteLine($"    code: {acc.Value.Code.Length} bytes");
                _output.WriteLine($"    storage: {acc.Value.Storage.Count} slots");
            }

            _output.WriteLine($"\n--- Genesis Block ---");
            _output.WriteLine($"  StateRoot: {test.GenesisBlockHeader.StateRoot.ToHex()}");
            _output.WriteLine($"  TxRoot: {test.GenesisBlockHeader.TransactionsRoot.ToHex()}");
            _output.WriteLine($"  ReceiptRoot: {test.GenesisBlockHeader.ReceiptsRoot.ToHex()}");
            _output.WriteLine($"  BaseFee: {test.GenesisBlockHeader.BaseFee}");
            _output.WriteLine($"  GasLimit: {test.GenesisBlockHeader.GasLimit}");

            _output.WriteLine($"\n--- Blocks ({test.Blocks.Count}) ---");
            foreach (var block in test.Blocks)
            {
                _output.WriteLine($"  Block {block.BlockNumber}:");
                _output.WriteLine($"    TxCount: {block.Transactions.Count}");
                _output.WriteLine($"    StateRoot: {block.BlockHeader.StateRoot.ToHex()}");
                _output.WriteLine($"    TxRoot: {block.BlockHeader.TransactionsRoot.ToHex()}");
                _output.WriteLine($"    ReceiptRoot: {block.BlockHeader.ReceiptsRoot.ToHex()}");
                _output.WriteLine($"    GasUsed: {block.BlockHeader.GasUsed}");

                foreach (var tx in block.Transactions)
                {
                    _output.WriteLine($"    TX: from={tx.Sender} to={tx.To ?? "CREATE"} value={tx.Value} gas={tx.GasLimit}");
                }
            }

            _output.WriteLine($"\n--- Post-State ({test.PostState.Count} accounts) ---");
            foreach (var acc in test.PostState)
            {
                _output.WriteLine($"  {acc.Key}:");
                _output.WriteLine($"    balance: {acc.Value.Balance}");
                _output.WriteLine($"    nonce: {acc.Value.Nonce}");
            }

            _output.WriteLine($"\n--- Expected Last Block Hash ---");
            _output.WriteLine($"  {test.LastBlockHash.ToHex()}");

            _output.WriteLine("\n\n=== Starting Execution ===\n");
            await ExecuteAndValidateTestAsync(test);
        }
    }
}
