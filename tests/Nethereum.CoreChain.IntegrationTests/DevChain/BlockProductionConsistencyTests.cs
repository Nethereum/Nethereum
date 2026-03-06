using System.Numerics;
using Nethereum.Contracts;
using Nethereum.CoreChain.IntegrationTests.Contracts;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;
using Nethereum.Util.HashProviders;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.DevChain
{
    public class BlockProductionConsistencyTests : IClassFixture<DevChainNodeFixture>
    {
        private readonly DevChainNodeFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly Sha3Keccack _keccak = new();

        public BlockProductionConsistencyTests(DevChainNodeFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        [Trait("Category", "BlockConsistency")]
        public async Task BlockHash_MatchesReEncodedHeader()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success, "Transaction should succeed");

            var block = await _fixture.Node.GetLatestBlockAsync();
            Assert.NotNull(block);

            var storedHash = await _fixture.Node.GetBlockHashByNumberAsync(block.BlockNumber);
            Assert.NotNull(storedHash);

            var encoder = BlockHeaderEncoder.Current;
            var encoded = encoder.Encode(block);
            var recomputedHash = _keccak.CalculateHash(encoded);

            Assert.True(
                storedHash.ToHex().IsTheSameHex(recomputedHash.ToHex()),
                $"Block hash mismatch for block #{block.BlockNumber}.\n" +
                $"Stored:     {storedHash.ToHex()}\n" +
                $"Recomputed: {recomputedHash.ToHex()}");

            _output.WriteLine($"Block #{block.BlockNumber}: hash consistent ({storedHash.ToHex().Substring(0, 16)}...)");
        }

        [Fact]
        [Trait("Category", "BlockConsistency")]
        public async Task BlockHash_ConsistentAcrossMultipleBlocks()
        {
            var blockHashes = new List<(BigInteger number, string stored, string computed)>();

            for (int i = 0; i < 5; i++)
            {
                var signedTx = _fixture.CreateSignedTransaction(
                    _fixture.RecipientAddress,
                    BigInteger.Parse("10000000000000000"));

                var result = await _fixture.Node.SendTransactionAsync(signedTx);
                Assert.True(result.Success);

                var block = await _fixture.Node.GetLatestBlockAsync();
                var storedHash = await _fixture.Node.GetBlockHashByNumberAsync(block.BlockNumber);
                var recomputedHash = _keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(block));

                Assert.True(
                    storedHash.ToHex().IsTheSameHex(recomputedHash.ToHex()),
                    $"Block hash mismatch at block #{block.BlockNumber}");

                blockHashes.Add((block.BlockNumber, storedHash.ToHex(), recomputedHash.ToHex()));
            }

            foreach (var (number, stored, computed) in blockHashes)
            {
                _output.WriteLine($"  Block #{number}: {stored.Substring(0, 16)}... OK");
            }
        }

        [Fact]
        [Trait("Category", "BlockConsistency")]
        public async Task BlockHash_EmptyBlockConsistent()
        {
            var blockHash = await _fixture.Node.MineBlockAsync();
            var block = await _fixture.Node.GetBlockByHashAsync(blockHash);
            Assert.NotNull(block);

            var recomputedHash = _keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(block));

            Assert.True(
                blockHash.ToHex().IsTheSameHex(recomputedHash.ToHex()),
                $"Empty block hash mismatch.\nStored: {blockHash.ToHex()}\nRecomputed: {recomputedHash.ToHex()}");

            _output.WriteLine($"Empty block #{block.BlockNumber}: hash consistent");
        }

        [Fact]
        [Trait("Category", "BlockConsistency")]
        public async Task BlockHash_HeaderRoundTripPreservesHash()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));

            await _fixture.Node.SendTransactionAsync(signedTx);
            var block = await _fixture.Node.GetLatestBlockAsync();

            var encoder = BlockHeaderEncoder.Current;
            var encoded = encoder.Encode(block);
            var decoded = encoder.Decode(encoded);
            var reEncoded = encoder.Encode(decoded);

            Assert.Equal(encoded.ToHex(), reEncoded.ToHex());

            var hash1 = _keccak.CalculateHash(encoded);
            var hash2 = _keccak.CalculateHash(reEncoded);
            Assert.Equal(hash1.ToHex(), hash2.ToHex());

            _output.WriteLine($"Block #{block.BlockNumber}: encode→decode→encode round-trip preserves hash");
        }

        [Fact]
        [Trait("Category", "BlockConsistency")]
        public async Task ParentHash_ChainLinksCorrectly()
        {
            var hash1 = await _fixture.Node.MineBlockAsync();
            var block1 = await _fixture.Node.GetBlockByHashAsync(hash1);

            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("50000000000000000"));
            await _fixture.Node.SendTransactionAsync(signedTx);

            var block2 = await _fixture.Node.GetLatestBlockAsync();
            var hash2 = await _fixture.Node.GetBlockHashByNumberAsync(block2.BlockNumber);

            Assert.Equal(hash1.ToHex(), block2.ParentHash.ToHex());

            var recomputedHash1 = _keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(block1));
            Assert.Equal(recomputedHash1.ToHex(), block2.ParentHash.ToHex());

            _output.WriteLine($"Block #{block2.BlockNumber} parentHash points to #{block1.BlockNumber}: chain links correctly");
        }

        [Fact]
        [Trait("Category", "BlockConsistency")]
        public async Task StateRoot_MatchesIndependentComputation()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var block = await _fixture.Node.GetLatestBlockAsync();
            Assert.NotNull(block.StateRoot);

            var stateRootCalc = new StateRootCalculator();
            var independentRoot = await stateRootCalc.ComputeStateRootAsync(
                _fixture.Node.State, _fixture.Node.TrieNodes);

            Assert.True(
                block.StateRoot.ToHex().IsTheSameHex(independentRoot.ToHex()),
                $"State root mismatch for block #{block.BlockNumber}.\n" +
                $"Block header: {block.StateRoot.ToHex()}\n" +
                $"Recomputed:   {independentRoot.ToHex()}");

            _output.WriteLine($"Block #{block.BlockNumber}: state root consistent ({block.StateRoot.ToHex().Substring(0, 16)}...)");
        }

        [Fact]
        [Trait("Category", "BlockConsistency")]
        public async Task StateRoot_ChangesAfterTransaction()
        {
            var blockBefore = await _fixture.Node.GetLatestBlockAsync();
            var rootBefore = blockBefore.StateRoot.ToHex();

            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("200000000000000000"));

            await _fixture.Node.SendTransactionAsync(signedTx);

            var blockAfter = await _fixture.Node.GetLatestBlockAsync();
            var rootAfter = blockAfter.StateRoot.ToHex();

            Assert.NotEqual(rootBefore, rootAfter);

            var stateRootCalc = new StateRootCalculator();
            var independentRoot = await stateRootCalc.ComputeStateRootAsync(
                _fixture.Node.State, _fixture.Node.TrieNodes);
            Assert.Equal(rootAfter, independentRoot.ToHex());

            _output.WriteLine($"State root changed: {rootBefore.Substring(0, 16)}... → {rootAfter.Substring(0, 16)}...");
        }

        [Fact]
        [Trait("Category", "BlockConsistency")]
        public async Task StateRoot_ContractDeploymentUpdatesCorrectly()
        {
            var contractAddress = await _fixture.DeployERC20Async();
            Assert.False(string.IsNullOrEmpty(contractAddress));

            var block = await _fixture.Node.GetLatestBlockAsync();
            var stateRootCalc = new StateRootCalculator();
            var independentRoot = await stateRootCalc.ComputeStateRootAsync(
                _fixture.Node.State, _fixture.Node.TrieNodes);

            Assert.True(
                block.StateRoot.ToHex().IsTheSameHex(independentRoot.ToHex()),
                $"State root mismatch after contract deployment.\n" +
                $"Block header: {block.StateRoot.ToHex()}\n" +
                $"Recomputed:   {independentRoot.ToHex()}");

            _output.WriteLine($"Contract deployed at {contractAddress}, state root consistent");
        }

        [Fact]
        [Trait("Category", "BlockConsistency")]
        public async Task TransactionRoot_MatchesRecomputedTrie()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));
            var encodedTx = signedTx.GetRLPEncoded();

            var sendResult = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(sendResult.Success);

            var block = await _fixture.Node.GetLatestBlockAsync();
            var blockHash = await _fixture.Node.GetBlockHashByNumberAsync(block.BlockNumber);

            var storedTxs = await _fixture.Node.Transactions.GetByBlockHashAsync(blockHash);
            Assert.NotNull(storedTxs);
            Assert.True(storedTxs.Count > 0, "Block should contain transactions");

            var rootCalculator = new RootCalculator();
            var encodedTxs = storedTxs.Select(tx => tx.GetRLPEncoded()).ToList();
            var recomputedRoot = rootCalculator.CalculateTransactionsRoot(encodedTxs);

            Assert.True(
                block.TransactionsHash.ToHex().IsTheSameHex(recomputedRoot.ToHex()),
                $"Transaction root mismatch for block #{block.BlockNumber}.\n" +
                $"Block header: {block.TransactionsHash.ToHex()}\n" +
                $"Recomputed:   {recomputedRoot.ToHex()}");

            _output.WriteLine($"Block #{block.BlockNumber}: tx root consistent ({storedTxs.Count} txs)");
        }

        [Fact]
        [Trait("Category", "BlockConsistency")]
        public async Task TransactionOrder_PreservedInBlock()
        {
            var nonce1 = await _fixture.Node.GetNonceAsync(_fixture.Address);
            var nonce2 = nonce1 + 1;
            var nonce3 = nonce2 + 1;

            var tx1 = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("10000000000000000"),
                nonce: nonce1);
            var tx2 = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("20000000000000000"),
                nonce: nonce2);
            var tx3 = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("30000000000000000"),
                nonce: nonce3);

            _fixture.Node.BlockManager.AddPendingTransaction(tx1);
            _fixture.Node.BlockManager.AddPendingTransaction(tx2);
            _fixture.Node.BlockManager.AddPendingTransaction(tx3);
            var blockHash = await _fixture.Node.MineBlockAsync();

            var block = await _fixture.Node.GetBlockByHashAsync(blockHash);
            Assert.NotNull(block);

            var storedTxs = await _fixture.Node.Transactions.GetByBlockHashAsync(blockHash);
            Assert.NotNull(storedTxs);
            Assert.Equal(3, storedTxs.Count);

            Assert.Equal(tx1.Hash.ToHex(), storedTxs[0].Hash.ToHex());
            Assert.Equal(tx2.Hash.ToHex(), storedTxs[1].Hash.ToHex());
            Assert.Equal(tx3.Hash.ToHex(), storedTxs[2].Hash.ToHex());

            _output.WriteLine($"Transaction order preserved: nonce {nonce1} → {nonce2} → {nonce3}");
        }

        [Fact]
        [Trait("Category", "BlockConsistency")]
        public async Task TransactionOrder_ReceiptsMatchTransactions()
        {
            var nonce1 = await _fixture.Node.GetNonceAsync(_fixture.Address);
            var nonce2 = nonce1 + 1;

            var tx1 = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("10000000000000000"),
                nonce: nonce1);
            var tx2 = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("20000000000000000"),
                nonce: nonce2);

            _fixture.Node.BlockManager.AddPendingTransaction(tx1);
            _fixture.Node.BlockManager.AddPendingTransaction(tx2);
            var blockHash = await _fixture.Node.MineBlockAsync();

            var block = await _fixture.Node.GetBlockByHashAsync(blockHash);
            Assert.NotNull(block);

            var storedTxs = await _fixture.Node.Transactions.GetByBlockHashAsync(blockHash);
            var storedReceipts = await _fixture.Node.Receipts.GetByBlockHashAsync(blockHash);

            Assert.NotNull(storedTxs);
            Assert.NotNull(storedReceipts);
            Assert.Equal(2, storedTxs.Count);
            Assert.Equal(storedTxs.Count, storedReceipts.Count);

            for (int i = 0; i < storedReceipts.Count; i++)
            {
                Assert.True(storedReceipts[i].HasSucceeded == true,
                    $"Receipt {i} should indicate success");
            }

            Assert.True(
                storedReceipts[0].CumulativeGasUsed <= storedReceipts[1].CumulativeGasUsed,
                "Cumulative gas should be non-decreasing across receipts");

            _output.WriteLine($"Block #{block.BlockNumber}: {storedTxs.Count} txs match {storedReceipts.Count} receipts");
        }

        [Fact]
        [Trait("Category", "BlockConsistency")]
        public async Task ReceiptRoot_MatchesRecomputedTrie()
        {
            var signedTx = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("100000000000000000"));

            await _fixture.Node.SendTransactionAsync(signedTx);

            var block = await _fixture.Node.GetLatestBlockAsync();
            var blockHash = await _fixture.Node.GetBlockHashByNumberAsync(block.BlockNumber);

            var storedReceipts = await _fixture.Node.Receipts.GetByBlockHashAsync(blockHash);
            Assert.NotNull(storedReceipts);
            Assert.True(storedReceipts.Count > 0);

            var rootCalculator = new RootCalculator();
            var recomputedRoot = rootCalculator.CalculateReceiptsRoot(storedReceipts);

            Assert.True(
                block.ReceiptHash.ToHex().IsTheSameHex(recomputedRoot.ToHex()),
                $"Receipt root mismatch for block #{block.BlockNumber}.\n" +
                $"Block header: {block.ReceiptHash.ToHex()}\n" +
                $"Recomputed:   {recomputedRoot.ToHex()}");

            _output.WriteLine($"Block #{block.BlockNumber}: receipt root consistent ({storedReceipts.Count} receipts)");
        }

        [Fact]
        [Trait("Category", "BlockConsistency")]
        public async Task TransactionOrder_MultiSenderPreservesOrder()
        {
            var nonce1 = await _fixture.Node.GetNonceAsync(_fixture.Address);
            var nonce2 = await _fixture.Node.GetNonceAsync(_fixture.Address2);

            var txFromSender1 = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("10000000000000000"),
                nonce: nonce1);
            var txFromSender2 = _fixture.CreateSignedTransactionFrom2(
                _fixture.RecipientAddress,
                BigInteger.Parse("20000000000000000"),
                nonce: nonce2);

            _fixture.Node.BlockManager.AddPendingTransaction(txFromSender1);
            _fixture.Node.BlockManager.AddPendingTransaction(txFromSender2);
            var blockHash = await _fixture.Node.MineBlockAsync();

            var block = await _fixture.Node.GetBlockByHashAsync(blockHash);
            Assert.NotNull(block);

            var storedTxs = await _fixture.Node.Transactions.GetByBlockHashAsync(blockHash);
            Assert.NotNull(storedTxs);
            Assert.Equal(2, storedTxs.Count);

            Assert.Equal(txFromSender1.Hash.ToHex(), storedTxs[0].Hash.ToHex());
            Assert.Equal(txFromSender2.Hash.ToHex(), storedTxs[1].Hash.ToHex());

            var rootCalculator = new RootCalculator();
            var encodedTxs = storedTxs.Select(tx => tx.GetRLPEncoded()).ToList();
            var recomputedRoot = rootCalculator.CalculateTransactionsRoot(encodedTxs);

            Assert.True(
                block.TransactionsHash.ToHex().IsTheSameHex(recomputedRoot.ToHex()),
                "Transaction root should match with multi-sender ordering");

            _output.WriteLine($"Multi-sender ordering: sender1 tx first, sender2 tx second, root consistent");
        }

        [Fact]
        [Trait("Category", "BlockConsistency")]
        public async Task TransactionOrder_ContractCallsPreserveReceiptOrder()
        {
            var contractAddress = await _fixture.DeployERC20Async(BigInteger.Parse("1000000000000000000000"));

            var nonce = await _fixture.Node.GetNonceAsync(_fixture.Address);

            var transferFunc1 = new TransferFunction { To = _fixture.RecipientAddress, Value = 100 };
            var transferFunc2 = new TransferFunction { To = _fixture.Address2, Value = 200 };
            var transferFunc3 = new TransferFunction { To = _fixture.RecipientAddress, Value = 300 };

            var tx1 = _fixture.CreateSignedTransaction(contractAddress, BigInteger.Zero, transferFunc1.GetCallData(), nonce: nonce);
            var tx2 = _fixture.CreateSignedTransaction(contractAddress, BigInteger.Zero, transferFunc2.GetCallData(), nonce: nonce + 1);
            var tx3 = _fixture.CreateSignedTransaction(contractAddress, BigInteger.Zero, transferFunc3.GetCallData(), nonce: nonce + 2);

            _fixture.Node.BlockManager.AddPendingTransaction(tx1);
            _fixture.Node.BlockManager.AddPendingTransaction(tx2);
            _fixture.Node.BlockManager.AddPendingTransaction(tx3);
            var blockHash = await _fixture.Node.MineBlockAsync();

            var block = await _fixture.Node.GetBlockByHashAsync(blockHash);
            Assert.NotNull(block);

            var storedTxs = await _fixture.Node.Transactions.GetByBlockHashAsync(blockHash);
            var storedReceipts = await _fixture.Node.Receipts.GetByBlockHashAsync(blockHash);

            Assert.Equal(3, storedTxs.Count);
            Assert.Equal(storedTxs.Count, storedReceipts.Count);

            Assert.Equal(tx1.Hash.ToHex(), storedTxs[0].Hash.ToHex());
            Assert.Equal(tx2.Hash.ToHex(), storedTxs[1].Hash.ToHex());
            Assert.Equal(tx3.Hash.ToHex(), storedTxs[2].Hash.ToHex());

            for (int i = 1; i < storedReceipts.Count; i++)
            {
                Assert.True(
                    storedReceipts[i].CumulativeGasUsed >= storedReceipts[i - 1].CumulativeGasUsed,
                    $"Cumulative gas should be non-decreasing: receipt[{i - 1}]={storedReceipts[i - 1].CumulativeGasUsed}, receipt[{i}]={storedReceipts[i].CumulativeGasUsed}");
            }

            var rootCalculator = new RootCalculator();
            var encodedTxs = storedTxs.Select(tx => tx.GetRLPEncoded()).ToList();
            var txRoot = rootCalculator.CalculateTransactionsRoot(encodedTxs);
            var receiptRoot = rootCalculator.CalculateReceiptsRoot(storedReceipts);

            Assert.True(block.TransactionsHash.ToHex().IsTheSameHex(txRoot.ToHex()),
                "Transaction root should match after contract calls");
            Assert.True(block.ReceiptHash.ToHex().IsTheSameHex(receiptRoot.ToHex()),
                "Receipt root should match after contract calls");

            _output.WriteLine($"3 contract calls: tx order preserved, receipt order matches, both roots consistent");
        }

        [Fact]
        [Trait("Category", "BlockConsistency")]
        public async Task GasUsed_BlockHeaderMatchesSumOfReceipts()
        {
            var nonce = await _fixture.Node.GetNonceAsync(_fixture.Address);

            var tx1 = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("10000000000000000"),
                nonce: nonce);
            var tx2 = _fixture.CreateSignedTransaction(
                _fixture.RecipientAddress,
                BigInteger.Parse("20000000000000000"),
                nonce: nonce + 1);

            _fixture.Node.BlockManager.AddPendingTransaction(tx1);
            _fixture.Node.BlockManager.AddPendingTransaction(tx2);
            var blockHash = await _fixture.Node.MineBlockAsync();

            var block = await _fixture.Node.GetBlockByHashAsync(blockHash);
            Assert.NotNull(block);

            var storedReceipts = await _fixture.Node.Receipts.GetByBlockHashAsync(blockHash);
            Assert.NotNull(storedReceipts);
            Assert.Equal(2, storedReceipts.Count);

            var lastReceipt = storedReceipts.Last();
            Assert.Equal((long)lastReceipt.CumulativeGasUsed, block.GasUsed);

            Assert.True(storedReceipts[0].CumulativeGasUsed < storedReceipts[1].CumulativeGasUsed,
                "Cumulative gas should increase across 2 transfer receipts");

            _output.WriteLine($"Block #{block.BlockNumber}: GasUsed={block.GasUsed} matches last receipt cumulative gas ({storedReceipts.Count} receipts)");
        }

        [Fact]
        [Trait("Category", "BlockConsistency")]
        public async Task FullBlockIntegrity_AllRootsConsistent()
        {
            var contractAddress = await _fixture.DeployERC20Async(BigInteger.Parse("1000000000000000000000"));

            var nonce = await _fixture.Node.GetNonceAsync(_fixture.Address);
            var transferFunc = new TransferFunction { To = _fixture.RecipientAddress, Value = 500 };
            var signedTx = _fixture.CreateSignedTransaction(
                contractAddress, BigInteger.Zero,
                transferFunc.GetCallData(), nonce: nonce);

            await _fixture.Node.SendTransactionAsync(signedTx);

            var block = await _fixture.Node.GetLatestBlockAsync();
            var blockHash = await _fixture.Node.GetBlockHashByNumberAsync(block.BlockNumber);
            Assert.NotNull(block);

            var recomputedBlockHash = _keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(block));
            Assert.True(blockHash.ToHex().IsTheSameHex(recomputedBlockHash.ToHex()),
                "Block hash should be self-consistent");

            var storedTxs = await _fixture.Node.Transactions.GetByBlockHashAsync(blockHash);
            var rootCalc = new RootCalculator();
            var txRoot = rootCalc.CalculateTransactionsRoot(
                storedTxs.Select(tx => tx.GetRLPEncoded()).ToList());
            Assert.True(block.TransactionsHash.ToHex().IsTheSameHex(txRoot.ToHex()),
                "Transaction root should be self-consistent");

            var storedReceipts = await _fixture.Node.Receipts.GetByBlockHashAsync(blockHash);
            var receiptRoot = rootCalc.CalculateReceiptsRoot(storedReceipts);
            Assert.True(block.ReceiptHash.ToHex().IsTheSameHex(receiptRoot.ToHex()),
                "Receipt root should be self-consistent");

            var stateRootCalc = new StateRootCalculator();
            var stateRoot = await stateRootCalc.ComputeStateRootAsync(
                _fixture.Node.State, _fixture.Node.TrieNodes);
            Assert.True(block.StateRoot.ToHex().IsTheSameHex(stateRoot.ToHex()),
                "State root should be self-consistent");

            Assert.Equal((long)storedReceipts.Last().CumulativeGasUsed, block.GasUsed);

            _output.WriteLine($"Block #{block.BlockNumber}: FULL INTEGRITY CHECK PASSED");
            _output.WriteLine($"  Block hash:   {blockHash.ToHex().Substring(0, 20)}...");
            _output.WriteLine($"  Tx root:      {block.TransactionsHash.ToHex().Substring(0, 20)}... ({storedTxs.Count} txs)");
            _output.WriteLine($"  Receipt root: {block.ReceiptHash.ToHex().Substring(0, 20)}... ({storedReceipts.Count} receipts)");
            _output.WriteLine($"  State root:   {block.StateRoot.ToHex().Substring(0, 20)}...");
            _output.WriteLine($"  Gas used:     {block.GasUsed}");
        }
    }
}
