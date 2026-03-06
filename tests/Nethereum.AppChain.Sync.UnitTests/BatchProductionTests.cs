using System.Numerics;
using Nethereum.AppChain.Sequencer;
using Nethereum.AppChain.Sync;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Signer.Crypto;
using Xunit;

namespace Nethereum.AppChain.Sync.UnitTests
{
    public class BatchProductionTests
    {
        private readonly BigInteger _chainId = 420420;

        [Fact]
        public void IsBatchDue_ReturnsFalse_WhenNotEnabled()
        {
            var config = new BatchProductionConfig { Enabled = false, BatchCadence = 10 };
            var batchProducer = CreateBatchProducer(config);

            Assert.False(batchProducer.IsBatchDue(9));
            Assert.False(batchProducer.IsBatchDue(99));
        }

        [Fact]
        public void IsBatchDue_ReturnsTrue_AtCadenceBoundary()
        {
            var config = new BatchProductionConfig { Enabled = true, BatchCadence = 10 };
            var batchProducer = CreateBatchProducer(config);

            Assert.True(batchProducer.IsBatchDue(9));
            Assert.False(batchProducer.IsBatchDue(8));
            Assert.False(batchProducer.IsBatchDue(10));
            Assert.True(batchProducer.IsBatchDue(19));
            Assert.True(batchProducer.IsBatchDue(99));
        }

        [Fact]
        public async Task ProduceBatchAsync_CreatesBatchFile()
        {
            var (blockStore, txStore, receiptStore) = await CreateAndPopulateStoresAsync(10);
            var batchStore = new InMemoryBatchStore();
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            var config = new BatchProductionConfig
            {
                Enabled = true,
                BatchCadence = 10,
                BatchOutputDirectory = tempDir,
                CompressBatches = true
            };

            var batchProducer = new SequencerBatchProducer(
                blockStore, txStore, receiptStore, batchStore, config, _chainId);

            var result = await batchProducer.ProduceBatchAsync(0, 9);

            Assert.True(result.Success);
            Assert.NotNull(result.BatchInfo);
            Assert.Equal(0, result.BatchInfo.FromBlock);
            Assert.Equal(9, result.BatchInfo.ToBlock);
            Assert.True(File.Exists(result.FilePath));

            Directory.Delete(tempDir, true);
        }

        [Fact]
        public async Task ProduceBatchIfDueAsync_ProducesBatchOnlyWhenDue()
        {
            var (blockStore, txStore, receiptStore) = await CreateAndPopulateStoresAsync(20);
            var batchStore = new InMemoryBatchStore();
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            var config = new BatchProductionConfig
            {
                Enabled = true,
                BatchCadence = 10,
                BatchOutputDirectory = tempDir,
                CompressBatches = true
            };

            var batchProducer = new SequencerBatchProducer(
                blockStore, txStore, receiptStore, batchStore, config, _chainId);

            var result1 = await batchProducer.ProduceBatchIfDueAsync(5);
            Assert.False(result1.Success);

            var result2 = await batchProducer.ProduceBatchIfDueAsync(9);
            Assert.True(result2.Success);
            Assert.Equal(0, result2.BatchInfo!.FromBlock);
            Assert.Equal(9, result2.BatchInfo.ToBlock);

            var result3 = await batchProducer.ProduceBatchIfDueAsync(15);
            Assert.False(result3.Success);

            var result4 = await batchProducer.ProduceBatchIfDueAsync(19);
            Assert.True(result4.Success);
            Assert.Equal(10, result4.BatchInfo!.FromBlock);
            Assert.Equal(19, result4.BatchInfo.ToBlock);

            Directory.Delete(tempDir, true);
        }

        [Fact]
        public async Task ProduceBatchAsync_UpdatesBatchStore()
        {
            var (blockStore, txStore, receiptStore) = await CreateAndPopulateStoresAsync(10);
            var batchStore = new InMemoryBatchStore();
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            var config = new BatchProductionConfig
            {
                Enabled = true,
                BatchCadence = 10,
                BatchOutputDirectory = tempDir,
                CompressBatches = true
            };

            var batchProducer = new SequencerBatchProducer(
                blockStore, txStore, receiptStore, batchStore, config, _chainId);

            await batchProducer.ProduceBatchAsync(0, 9);

            var storedBatch = await batchStore.GetBatchAsync(0, 9);
            Assert.NotNull(storedBatch);
            Assert.Equal(BatchStatus.Written, storedBatch.Status);

            Directory.Delete(tempDir, true);
        }

        [Fact]
        public async Task BatchProducer_FiresEvent_WhenBatchProduced()
        {
            var (blockStore, txStore, receiptStore) = await CreateAndPopulateStoresAsync(10);
            var batchStore = new InMemoryBatchStore();
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            var config = new BatchProductionConfig
            {
                Enabled = true,
                BatchCadence = 10,
                BatchOutputDirectory = tempDir
            };

            var batchProducer = new SequencerBatchProducer(
                blockStore, txStore, receiptStore, batchStore, config, _chainId);

            BatchProductionResult? eventResult = null;
            batchProducer.BatchProduced += (sender, result) => eventResult = result;

            await batchProducer.ProduceBatchAsync(0, 9);

            Assert.NotNull(eventResult);
            Assert.True(eventResult!.Success);
            Assert.Equal(0, eventResult.BatchInfo!.FromBlock);

            Directory.Delete(tempDir, true);
        }

        [Fact]
        public async Task IsTimeThresholdExceeded_ReturnsFalse_WhenThresholdZero()
        {
            var config = new BatchProductionConfig
            {
                Enabled = true,
                BatchCadence = 100,
                TimeThresholdSeconds = 0
            };
            var batchProducer = CreateBatchProducer(config);

            Assert.False(batchProducer.IsTimeThresholdExceeded(50));
        }

        [Fact]
        public async Task IsTimeThresholdExceeded_ReturnsFalse_WhenNotEnoughTimeElapsed()
        {
            var (blockStore, txStore, receiptStore) = await CreateAndPopulateStoresAsync(10);
            var batchStore = new InMemoryBatchStore();
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            var config = new BatchProductionConfig
            {
                Enabled = true,
                BatchCadence = 100,
                TimeThresholdSeconds = 60,
                BatchOutputDirectory = tempDir
            };

            var batchProducer = new SequencerBatchProducer(
                blockStore, txStore, receiptStore, batchStore, config, _chainId);

            await batchProducer.InitializeAsync();

            Assert.False(batchProducer.IsTimeThresholdExceeded(5));

            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }

        [Fact]
        public void IsBatchDue_CombinesCadenceAndTimeThreshold()
        {
            var config = new BatchProductionConfig
            {
                Enabled = true,
                BatchCadence = 100,
                TimeThresholdSeconds = 30
            };
            var batchProducer = CreateBatchProducer(config);

            Assert.True(batchProducer.IsBatchDue(99));
            Assert.False(batchProducer.IsBatchDue(50));
        }

        [Fact]
        public void BatchProductionConfig_WithTimeThreshold_CreatesCorrectConfig()
        {
            var config = BatchProductionConfig.WithTimeThreshold(50, 120);

            Assert.True(config.Enabled);
            Assert.Equal(50, config.BatchCadence);
            Assert.Equal(120, config.TimeThresholdSeconds);
            Assert.True(config.CompressBatches);
        }

        private SequencerBatchProducer CreateBatchProducer(BatchProductionConfig config)
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var batchStore = new InMemoryBatchStore();

            return new SequencerBatchProducer(
                blockStore, txStore, receiptStore, batchStore, config, _chainId);
        }

        private async Task<(InMemoryBlockStore, InMemoryTransactionStore, InMemoryReceiptStore)> CreateAndPopulateStoresAsync(int blockCount)
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();

            var prevHash = new byte[32];
            var key = EthECKey.GenerateKey();
            var keccak = new Nethereum.Util.Sha3Keccack();

            for (int i = 0; i < blockCount; i++)
            {
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

                var header = new BlockHeader
                {
                    ParentHash = prevHash,
                    UnclesHash = new byte[32],
                    Coinbase = "0x0000000000000000000000000000000000000001",
                    StateRoot = new byte[32],
                    TransactionsHash = new byte[32],
                    ReceiptHash = new byte[32],
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

                var blockHash = keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(header));
                await blockStore.SaveAsync(header, blockHash);

                for (int t = 0; t < transactions.Count; t++)
                {
                    var tx = transactions[t];
                    await txStore.SaveAsync(tx, blockHash, t, i);

                    var receipt = Receipt.CreateStatusReceipt(true, (t + 1) * 21000, new byte[256], new List<Log>());
                    await receiptStore.SaveAsync(receipt, tx.Hash, blockHash, i, t, 21000, null, 1000000000);
                }

                prevHash = blockHash;
            }

            return (blockStore, txStore, receiptStore);
        }
    }
}
