using System;
using System.Linq;
using System.Numerics;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Signer;
using Nethereum.Signer.Crypto;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AppChain.Sync.UnitTests
{
    public class EndToEndSyncTests
    {
        private readonly BigInteger _chainId = 420420;
        private static readonly RootCalculator _rootCalculator = new RootCalculator();
        private static readonly Sha3Keccack _keccak = new Sha3Keccack();

        [Fact]
        public async Task FullSyncFlow_SequencerToReplica_SyncsSuccessfully()
        {
            // Arrange - Setup sequencer stores (source)
            var sequencerBlockStore = new InMemoryBlockStore();
            var sequencerTxStore = new InMemoryTransactionStore(sequencerBlockStore);
            var sequencerReceiptStore = new InMemoryReceiptStore();
            var sequencerLogStore = new InMemoryLogStore();
            var sequencerStateStore = new InMemoryStateStore();

            // Populate sequencer with blocks
            await PopulateSequencerWithBlocks(sequencerBlockStore, sequencerTxStore, sequencerReceiptStore, 10);

            // Arrange - Setup replica stores (destination)
            var replicaBlockStore = new InMemoryBlockStore();
            var replicaTxStore = new InMemoryTransactionStore(replicaBlockStore);
            var replicaReceiptStore = new InMemoryReceiptStore();
            var replicaLogStore = new InMemoryLogStore();
            var replicaBatchStore = new InMemoryBatchStore();

            // Create batch from sequencer data
            var writer = new BatchFileWriter();
            var blocks = await CollectBlocksForBatch(sequencerBlockStore, sequencerTxStore, sequencerReceiptStore, 0, 9);

            using var batchStream = new MemoryStream();
            var batchInfo = await writer.WriteBatchAsync(batchStream, _chainId, blocks);

            // Act - Import batch to replica
            var importer = new BatchImporter(replicaBlockStore, replicaTxStore, replicaReceiptStore, replicaLogStore, replicaBatchStore);
            batchStream.Position = 0;
            var importResult = await importer.ImportBatchAsync(batchStream, batchInfo.BatchHash, BatchVerificationMode.Quick);

            // Assert
            Assert.True(importResult.Success);
            Assert.Equal(10, importResult.BlocksImported);

            // Verify replica has all blocks
            var replicaHeight = await replicaBlockStore.GetHeightAsync();
            Assert.Equal(9, replicaHeight);

            // Verify batch store is updated
            var latestImported = await replicaBatchStore.GetLatestImportedBlockAsync();
            Assert.Equal(9, latestImported);
        }

        [Fact]
        public async Task MultipleBatchSync_SyncsInOrder()
        {
            // Arrange
            var sequencerBlockStore = new InMemoryBlockStore();
            var sequencerTxStore = new InMemoryTransactionStore(sequencerBlockStore);
            var sequencerReceiptStore = new InMemoryReceiptStore();

            await PopulateSequencerWithBlocks(sequencerBlockStore, sequencerTxStore, sequencerReceiptStore, 30);

            var replicaBlockStore = new InMemoryBlockStore();
            var replicaTxStore = new InMemoryTransactionStore(replicaBlockStore);
            var replicaReceiptStore = new InMemoryReceiptStore();
            var replicaLogStore = new InMemoryLogStore();
            var replicaBatchStore = new InMemoryBatchStore();

            var writer = new BatchFileWriter();
            var importer = new BatchImporter(replicaBlockStore, replicaTxStore, replicaReceiptStore, replicaLogStore, replicaBatchStore);

            // Act - Create and import 3 batches of 10 blocks each
            for (int batchNum = 0; batchNum < 3; batchNum++)
            {
                var fromBlock = batchNum * 10;
                var toBlock = fromBlock + 9;

                var blocks = await CollectBlocksForBatch(sequencerBlockStore, sequencerTxStore, sequencerReceiptStore, fromBlock, toBlock);

                using var batchStream = new MemoryStream();
                var batchInfo = await writer.WriteBatchAsync(batchStream, _chainId, blocks);

                batchStream.Position = 0;
                var result = await importer.ImportBatchAsync(batchStream, batchInfo.BatchHash, BatchVerificationMode.Quick);

                Assert.True(result.Success, $"Batch {batchNum} failed: {result.ErrorMessage}");
                Assert.Equal(10, result.BlocksImported);
            }

            // Assert
            var replicaHeight = await replicaBlockStore.GetHeightAsync();
            Assert.Equal(29, replicaHeight);

            var batches = await replicaBatchStore.GetBatchesAfterAsync(0, 100);
            Assert.Equal(3, batches.Count);

            var latestImported = await replicaBatchStore.GetLatestImportedBlockAsync();
            Assert.Equal(29, latestImported);
        }

        [Fact]
        public async Task StateSnapshotAndBatchSync_CombinedFlow()
        {
            // Arrange - Setup sequencer
            var sequencerBlockStore = new InMemoryBlockStore();
            var sequencerTxStore = new InMemoryTransactionStore(sequencerBlockStore);
            var sequencerReceiptStore = new InMemoryReceiptStore();
            var sequencerStateStore = new InMemoryStateStore();

            await PopulateSequencerWithBlocks(sequencerBlockStore, sequencerTxStore, sequencerReceiptStore, 10);
            await PopulateStateStore(sequencerStateStore);

            // Create state snapshot at block 0
            var snapshotWriter = new StateSnapshotWriter(sequencerStateStore, sequencerBlockStore, _chainId);
            using var snapshotStream = new MemoryStream();
            var snapshotInfo = await snapshotWriter.WriteSnapshotAsync(0, snapshotStream);

            // Setup replica
            var replicaBlockStore = new InMemoryBlockStore();
            var replicaTxStore = new InMemoryTransactionStore(replicaBlockStore);
            var replicaReceiptStore = new InMemoryReceiptStore();
            var replicaLogStore = new InMemoryLogStore();
            var replicaStateStore = new InMemoryStateStore();
            var replicaBatchStore = new InMemoryBatchStore();

            // Act - Import state snapshot first
            var snapshotImporter = new StateSnapshotImporter(replicaStateStore);
            snapshotStream.Position = 0;
            var snapshotResult = await snapshotImporter.ImportSnapshotAsync(snapshotStream);

            Assert.True(snapshotResult.Success);
            Assert.Equal(3, snapshotResult.AccountsImported);

            // Then import blocks batch
            var writer = new BatchFileWriter();
            var blocks = await CollectBlocksForBatch(sequencerBlockStore, sequencerTxStore, sequencerReceiptStore, 0, 9);

            using var batchStream = new MemoryStream();
            var batchInfo = await writer.WriteBatchAsync(batchStream, _chainId, blocks);

            var batchImporter = new BatchImporter(replicaBlockStore, replicaTxStore, replicaReceiptStore, replicaLogStore, replicaBatchStore);
            batchStream.Position = 0;
            var batchResult = await batchImporter.ImportBatchAsync(batchStream, batchInfo.BatchHash, BatchVerificationMode.Quick);

            // Assert
            Assert.True(batchResult.Success);
            Assert.Equal(10, batchResult.BlocksImported);

            // Verify state is present (use normalized address without 0x prefix)
            var account = await replicaStateStore.GetAccountAsync("0000000000000000000000000000000000000001");
            Assert.NotNull(account);
            Assert.Equal(BigInteger.Parse("1000000000000000000"), account.Balance);
        }

        private async Task PopulateSequencerWithBlocks(IBlockStore blockStore, ITransactionStore txStore, IReceiptStore receiptStore, int count)
        {
            var prevHash = new byte[32];
            var key = EthECKey.GenerateKey();

            for (int i = 0; i < count; i++)
            {
                // Create transactions first
                var transactions = new List<ISignedTransaction>();
                for (int t = 0; t < 2; t++)
                {
                    var tx = new Transaction1559(
                        _chainId,
                        nonce: i * 2 + t,
                        maxPriorityFeePerGas: 1000000000,
                        maxFeePerGas: 2000000000,
                        gasLimit: 21000,
                        receiverAddress: "0x0000000000000000000000000000000000000002",
                        amount: 1000000000000000000,
                        data: "",
                        accessList: null);

                    var signature = key.SignAndCalculateYParityV(tx.RawHash);
                    tx.SetSignature(new Signature { R = signature.R, S = signature.S, V = signature.V });
                    transactions.Add(tx);
                }

                // Compute transactions root
                var transactionsHash = ComputeTransactionsRoot(transactions, _keccak);

                var header = new BlockHeader
                {
                    ParentHash = prevHash,
                    UnclesHash = _keccak.CalculateHash(RLP.RLP.EncodeList()),
                    Coinbase = "0x0000000000000000000000000000000000000001",
                    StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                    TransactionsHash = transactionsHash,
                    ReceiptHash = DefaultValues.EMPTY_TRIE_HASH,
                    LogsBloom = new byte[256],
                    Difficulty = 1,
                    BlockNumber = i,
                    GasLimit = 30000000,
                    GasUsed = 42000,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + i,
                    ExtraData = new byte[0],
                    MixHash = new byte[32],
                    Nonce = new byte[8],
                    BaseFee = 1000000000
                };

                var blockHash = _keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(header));
                await blockStore.SaveAsync(header, blockHash);

                // Save transactions and receipts
                for (int t = 0; t < transactions.Count; t++)
                {
                    var tx = transactions[t];
                    await txStore.SaveAsync(tx, blockHash, t, i);

                    var receipt = Receipt.CreateStatusReceipt(true, (t + 1) * 21000, new byte[256], new List<Log>());
                    await receiptStore.SaveAsync(receipt, tx.Hash, blockHash, i, t, 21000, null, 1000000000);
                }

                prevHash = blockHash;
            }
        }

        private byte[] ComputeTransactionsRoot(List<ISignedTransaction> transactions, Nethereum.Util.Sha3Keccack keccak)
        {
            if (transactions.Count == 0)
                return DefaultValues.EMPTY_TRIE_HASH;

            var encodedTxs = transactions.Select(tx => tx.GetRLPEncoded()).ToList();
            return _rootCalculator.CalculateTransactionsRoot(encodedTxs);
        }

        private async Task PopulateStateStore(InMemoryStateStore stateStore)
        {
            await stateStore.SaveAccountAsync("0x0000000000000000000000000000000000000001", new Account
            {
                Balance = BigInteger.Parse("1000000000000000000"),
                Nonce = 1,
                CodeHash = new byte[32],
                StateRoot = new byte[32]
            });

            await stateStore.SaveAccountAsync("0x0000000000000000000000000000000000000002", new Account
            {
                Balance = BigInteger.Parse("2000000000000000000"),
                Nonce = 5,
                CodeHash = new byte[32],
                StateRoot = new byte[32]
            });

            await stateStore.SaveAccountAsync("0x0000000000000000000000000000000000000003", new Account
            {
                Balance = 0,
                Nonce = 0,
                CodeHash = new byte[32],
                StateRoot = new byte[32]
            });
        }

        private async Task<List<BatchBlockData>> CollectBlocksForBatch(
            IBlockStore blockStore,
            ITransactionStore txStore,
            IReceiptStore receiptStore,
            int fromBlock,
            int toBlock)
        {
            var blocks = new List<BatchBlockData>();

            for (int i = fromBlock; i <= toBlock; i++)
            {
                var header = await blockStore.GetByNumberAsync(i);
                var blockHash = await blockStore.GetHashByNumberAsync(i);
                var transactions = await txStore.GetByBlockHashAsync(blockHash);
                var receipts = await receiptStore.GetByBlockNumberAsync(i);

                blocks.Add(new BatchBlockData
                {
                    Header = header,
                    Transactions = transactions,
                    Receipts = receipts
                });
            }

            return blocks;
        }
    }
}
