using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.AppChain.Sync;
using Nethereum.AppChain.Genesis;
using Nethereum.AppChain.Sequencer;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

using AppChainCore = Nethereum.AppChain.AppChain;

namespace Nethereum.AppChain.IntegrationTests
{
    [Collection("Sequential")]
    public class BatchSyncE2ETests : IAsyncLifetime, IDisposable
    {
        private AppChainCore? _sequencerChain;
        private AppChainCore? _replicaChain;
        private Sequencer.Sequencer? _sequencer;
        private string _batchOutputDir = "";

        private const string SequencerPrivateKey = "0x8da4ef21b864d2cc526dbdb2a120bd2874c36c9d0a1fb7f8c63d7f7a8b41de8f";
        private readonly string _sequencerAddress;
        private static readonly BigInteger ChainId = new BigInteger(420420);

        public BatchSyncE2ETests()
        {
            var sequencerKey = new EthECKey(SequencerPrivateKey);
            _sequencerAddress = sequencerKey.GetPublicAddress();
        }

        public async Task InitializeAsync()
        {
            _batchOutputDir = Path.Combine(Path.GetTempPath(), $"batch_e2e_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_batchOutputDir);

            var sequencerBlockStore = new InMemoryBlockStore();
            var sequencerTxStore = new InMemoryTransactionStore(sequencerBlockStore);
            var sequencerReceiptStore = new InMemoryReceiptStore();
            var sequencerLogStore = new InMemoryLogStore();
            var sequencerStateStore = new InMemoryStateStore();

            var sequencerConfig = AppChainConfig.CreateWithName("SequencerChain", (int)ChainId);
            sequencerConfig.SequencerAddress = _sequencerAddress;

            _sequencerChain = new AppChainCore(sequencerConfig, sequencerBlockStore, sequencerTxStore, sequencerReceiptStore, sequencerLogStore, sequencerStateStore);

            var genesisOptions = new GenesisOptions
            {
                PrefundedAddresses = new[] { _sequencerAddress },
                PrefundBalance = BigInteger.Parse("10000000000000000000000"),
                DeployCreate2Factory = false
            };
            await _sequencerChain.InitializeAsync(genesisOptions);

            var replicaBlockStore = new InMemoryBlockStore();
            var replicaTxStore = new InMemoryTransactionStore(replicaBlockStore);
            var replicaReceiptStore = new InMemoryReceiptStore();
            var replicaLogStore = new InMemoryLogStore();
            var replicaStateStore = new InMemoryStateStore();

            var replicaConfig = AppChainConfig.CreateWithName("ReplicaChain", (int)ChainId);
            _replicaChain = new AppChainCore(replicaConfig, replicaBlockStore, replicaTxStore, replicaReceiptStore, replicaLogStore, replicaStateStore);
        }

        public Task DisposeAsync()
        {
            Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _sequencer = null;
            _sequencerChain = null;
            _replicaChain = null;

            if (!string.IsNullOrEmpty(_batchOutputDir) && Directory.Exists(_batchOutputDir))
            {
                try { Directory.Delete(_batchOutputDir, true); } catch { }
            }
        }

        [Fact]
        public async Task E2E_SequencerProducesBatches_OnBlockCadence()
        {
            var sequencerConfig = new SequencerConfig
            {
                SequencerAddress = _sequencerAddress,
                BlockTimeMs = 0,
                MaxTransactionsPerBlock = 100,
                BlockProductionMode = BlockProductionMode.OnDemand,
                Policy = Nethereum.AppChain.Sequencer.PolicyConfig.OpenAccess,
                BatchProduction = new BatchProductionConfig
                {
                    Enabled = true,
                    BatchCadence = 5,
                    BatchOutputDirectory = _batchOutputDir,
                    CompressBatches = true
                }
            };

            _sequencer = new Sequencer.Sequencer(_sequencerChain!, sequencerConfig);
            await _sequencer.StartAsync();

            BatchProductionResult? batchResult = null;
            _sequencer.BatchProduced += (sender, result) => batchResult = result;

            for (int i = 0; i < 5; i++)
            {
                var tx = CreateSignedTransaction(nonce: i);
                await _sequencer.SubmitTransactionAsync(tx);
            }

            var blockNumber = await _sequencer.GetBlockNumberAsync();
            Assert.Equal(5, blockNumber);

            Assert.NotNull(batchResult);
            Assert.True(batchResult!.Success);
            Assert.Equal(0, batchResult.BatchInfo!.FromBlock);
            Assert.Equal(4, batchResult.BatchInfo.ToBlock);
            Assert.True(File.Exists(batchResult.FilePath));

            await _sequencer.StopAsync();
        }

        [Fact]
        public async Task E2E_ReplicaImportsBatch_FromSequencer()
        {
            var sequencerConfig = new SequencerConfig
            {
                SequencerAddress = _sequencerAddress,
                BlockTimeMs = 0,
                MaxTransactionsPerBlock = 100,
                BlockProductionMode = BlockProductionMode.OnDemand,
                Policy = Nethereum.AppChain.Sequencer.PolicyConfig.OpenAccess,
                BatchProduction = new BatchProductionConfig
                {
                    Enabled = true,
                    BatchCadence = 10,
                    BatchOutputDirectory = _batchOutputDir,
                    CompressBatches = true
                }
            };

            _sequencer = new Sequencer.Sequencer(_sequencerChain!, sequencerConfig);
            await _sequencer.StartAsync();

            BatchProductionResult? batchResult = null;
            _sequencer.BatchProduced += (sender, result) => batchResult = result;

            for (int i = 0; i < 10; i++)
            {
                var tx = CreateSignedTransaction(nonce: i);
                await _sequencer.SubmitTransactionAsync(tx);
            }

            Assert.NotNull(batchResult);
            Assert.True(File.Exists(batchResult!.FilePath));

            var replicaBatchStore = new InMemoryBatchStore();
            var importer = new BatchImporter(
                _replicaChain!.Blocks,
                _replicaChain.Transactions,
                _replicaChain.Receipts,
                _replicaChain.Logs,
                replicaBatchStore);

            var importResult = await importer.ImportBatchFromFileAsync(batchResult.FilePath!, batchResult.BatchInfo!.BatchHash, BatchVerificationMode.Quick, compressed: true);

            Assert.True(importResult.Success, $"Import failed: {importResult.ErrorMessage}");
            Assert.Equal(10, importResult.BlocksImported);

            var replicaHeight = await _replicaChain.Blocks.GetHeightAsync();
            Assert.Equal(9, replicaHeight);

            var latestImported = await replicaBatchStore.GetLatestImportedBlockAsync();
            Assert.Equal(9, latestImported);

            await _sequencer.StopAsync();
        }

        [Fact]
        public async Task E2E_MultipleBatches_SyncInOrder()
        {
            var sequencerConfig = new SequencerConfig
            {
                SequencerAddress = _sequencerAddress,
                BlockTimeMs = 0,
                MaxTransactionsPerBlock = 100,
                BlockProductionMode = BlockProductionMode.OnDemand,
                Policy = Nethereum.AppChain.Sequencer.PolicyConfig.OpenAccess,
                BatchProduction = new BatchProductionConfig
                {
                    Enabled = true,
                    BatchCadence = 5,
                    BatchOutputDirectory = _batchOutputDir,
                    CompressBatches = true
                }
            };

            _sequencer = new Sequencer.Sequencer(_sequencerChain!, sequencerConfig);
            await _sequencer.StartAsync();

            var batchResults = new System.Collections.Generic.List<BatchProductionResult>();
            _sequencer.BatchProduced += (sender, result) => batchResults.Add(result);

            for (int i = 0; i < 15; i++)
            {
                var tx = CreateSignedTransaction(nonce: i);
                await _sequencer.SubmitTransactionAsync(tx);
            }

            Assert.Equal(3, batchResults.Count);

            var replicaBatchStore = new InMemoryBatchStore();
            var importer = new BatchImporter(
                _replicaChain!.Blocks,
                _replicaChain.Transactions,
                _replicaChain.Receipts,
                _replicaChain.Logs,
                replicaBatchStore);

            foreach (var batch in batchResults)
            {
                var importResult = await importer.ImportBatchFromFileAsync(batch.FilePath!, batch.BatchInfo!.BatchHash, BatchVerificationMode.Quick, compressed: true);
                Assert.True(importResult.Success, $"Batch import failed: {importResult.ErrorMessage}");
            }

            var replicaHeight = await _replicaChain.Blocks.GetHeightAsync();
            Assert.Equal(14, replicaHeight);

            await _sequencer.StopAsync();
        }

        [Fact]
        public async Task E2E_OnDemandMode_ProducesBlocksImmediately()
        {
            var sequencerConfig = new SequencerConfig
            {
                SequencerAddress = _sequencerAddress,
                BlockTimeMs = 0,
                MaxTransactionsPerBlock = 100,
                BlockProductionMode = BlockProductionMode.OnDemand,
                Policy = Nethereum.AppChain.Sequencer.PolicyConfig.OpenAccess
            };

            _sequencer = new Sequencer.Sequencer(_sequencerChain!, sequencerConfig);
            await _sequencer.StartAsync();

            var initialBlock = await _sequencer.GetBlockNumberAsync();
            Assert.Equal(0, initialBlock);

            var tx1 = CreateSignedTransaction(nonce: 0);
            await _sequencer.SubmitTransactionAsync(tx1);

            var afterTx1 = await _sequencer.GetBlockNumberAsync();
            Assert.Equal(1, afterTx1);

            var tx2 = CreateSignedTransaction(nonce: 1);
            await _sequencer.SubmitTransactionAsync(tx2);

            var afterTx2 = await _sequencer.GetBlockNumberAsync();
            Assert.Equal(2, afterTx2);

            Assert.Equal(0, _sequencer.TxPool.PendingCount);

            await _sequencer.StopAsync();
        }

        [Fact]
        public async Task E2E_BatchWithTransactions_PreservesAllData()
        {
            var sequencerConfig = new SequencerConfig
            {
                SequencerAddress = _sequencerAddress,
                BlockTimeMs = 0,
                MaxTransactionsPerBlock = 100,
                BlockProductionMode = BlockProductionMode.OnDemand,
                Policy = Nethereum.AppChain.Sequencer.PolicyConfig.OpenAccess,
                BatchProduction = new BatchProductionConfig
                {
                    Enabled = true,
                    BatchCadence = 4,
                    BatchOutputDirectory = _batchOutputDir,
                    CompressBatches = true
                }
            };

            _sequencer = new Sequencer.Sequencer(_sequencerChain!, sequencerConfig);
            await _sequencer.StartAsync();

            BatchProductionResult? batchResult = null;
            _sequencer.BatchProduced += (sender, result) => batchResult = result;

            var txHashes = new System.Collections.Generic.List<byte[]>();
            for (int i = 0; i < 4; i++)
            {
                var tx = CreateSignedTransaction(nonce: i);
                var hash = await _sequencer.SubmitTransactionAsync(tx);
                txHashes.Add(hash);
            }

            Assert.NotNull(batchResult);

            var replicaBatchStore = new InMemoryBatchStore();
            var importer = new BatchImporter(
                _replicaChain!.Blocks,
                _replicaChain.Transactions,
                _replicaChain.Receipts,
                _replicaChain.Logs,
                replicaBatchStore);

            await importer.ImportBatchFromFileAsync(batchResult!.FilePath!, batchResult.BatchInfo!.BatchHash, BatchVerificationMode.Quick, compressed: true);

            for (int blockNum = 1; blockNum <= 3; blockNum++)
            {
                var sequencerBlock = await _sequencerChain!.Blocks.GetByNumberAsync(blockNum);
                var replicaBlock = await _replicaChain.Blocks.GetByNumberAsync(blockNum);

                Assert.NotNull(sequencerBlock);
                Assert.NotNull(replicaBlock);
                Assert.Equal(sequencerBlock.BlockNumber, replicaBlock.BlockNumber);
                Assert.Equal(sequencerBlock.StateRoot, replicaBlock.StateRoot);
                Assert.Equal(sequencerBlock.TransactionsHash, replicaBlock.TransactionsHash);
            }

            await _sequencer.StopAsync();
        }

        [Fact]
        public async Task E2E_FinalityTracking_MarksBatchBlocksAsFinalized()
        {
            var sequencerConfig = new SequencerConfig
            {
                SequencerAddress = _sequencerAddress,
                BlockTimeMs = 0,
                MaxTransactionsPerBlock = 100,
                BlockProductionMode = BlockProductionMode.OnDemand,
                Policy = Nethereum.AppChain.Sequencer.PolicyConfig.OpenAccess,
                BatchProduction = new BatchProductionConfig
                {
                    Enabled = true,
                    BatchCadence = 5,
                    BatchOutputDirectory = _batchOutputDir,
                    CompressBatches = true
                }
            };

            _sequencer = new Sequencer.Sequencer(_sequencerChain!, sequencerConfig);
            await _sequencer.StartAsync();

            BatchProductionResult? batchResult = null;
            _sequencer.BatchProduced += (sender, result) => batchResult = result;

            for (int i = 0; i < 5; i++)
            {
                var tx = CreateSignedTransaction(nonce: i);
                await _sequencer.SubmitTransactionAsync(tx);
            }

            Assert.NotNull(batchResult);

            var replicaBatchStore = new InMemoryBatchStore();
            var finalityTracker = new InMemoryFinalityTracker();
            var importer = new BatchImporter(
                _replicaChain!.Blocks,
                _replicaChain.Transactions,
                _replicaChain.Receipts,
                _replicaChain.Logs,
                replicaBatchStore);

            var importResult = await importer.ImportBatchFromFileAsync(batchResult!.FilePath!, batchResult.BatchInfo!.BatchHash, BatchVerificationMode.Quick, compressed: true);

            Assert.True(importResult.Success);

            await finalityTracker.MarkRangeAsFinalizedAsync(
                batchResult.BatchInfo.FromBlock,
                batchResult.BatchInfo.ToBlock);

            Assert.True(await finalityTracker.IsFinalizedAsync(0));
            Assert.True(await finalityTracker.IsFinalizedAsync(4));
            Assert.False(await finalityTracker.IsFinalizedAsync(5));

            Assert.Equal(4, finalityTracker.LastFinalizedBlock);

            await _sequencer.StopAsync();
        }

        private ISignedTransaction CreateSignedTransaction(int nonce = 0)
        {
            var privateKey = new EthECKey(SequencerPrivateKey);

            var transaction = new Transaction1559(
                chainId: ChainId,
                nonce: nonce,
                maxPriorityFeePerGas: BigInteger.Zero,
                maxFeePerGas: new BigInteger(1000000000),
                gasLimit: new BigInteger(21000),
                receiverAddress: "0x0000000000000000000000000000000000000001",
                amount: BigInteger.Zero,
                data: null,
                accessList: null
            );

            var signature = privateKey.SignAndCalculateYParityV(transaction.RawHash);
            transaction.SetSignature(new Signature { R = signature.R, S = signature.S, V = signature.V });

            return transaction;
        }
    }
}
