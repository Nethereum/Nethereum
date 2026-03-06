using System;
using System.Linq;
using System.Numerics;
using Nethereum.AppChain.Sync;
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
    public class BatchImporterTests
    {
        private readonly BigInteger _chainId = 420420;

        [Fact]
        public async Task ImportBatch_WithValidBatch_ImportsAllData()
        {
            // Arrange
            var (blockStore, txStore, receiptStore, logStore, batchStore) = CreateStores();
            var writer = new BatchFileWriter();
            var importer = new BatchImporter(blockStore, txStore, receiptStore, logStore, batchStore);

            var blocks = CreateTestBlocks(5);
            using var stream = new MemoryStream();
            var batchInfo = await writer.WriteBatchAsync(stream, _chainId, blocks);

            // Act
            stream.Position = 0;
            var result = await importer.ImportBatchAsync(stream, batchInfo.BatchHash, BatchVerificationMode.Quick);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(5, result.BlocksImported);
            Assert.Equal(10, result.TransactionsImported); // 2 tx per block
            Assert.NotNull(result.BatchInfo);
            Assert.Equal(BatchStatus.Imported, result.BatchInfo.Status);

            // Verify blocks were stored
            for (int i = 0; i < 5; i++)
            {
                var storedBlock = await blockStore.GetByNumberAsync(i);
                Assert.NotNull(storedBlock);
                Assert.Equal(i, (int)storedBlock.BlockNumber);
            }
        }

        [Fact]
        public async Task ImportBatch_WithQuickVerification_VerifiesHeaderChain()
        {
            // Arrange
            var (blockStore, txStore, receiptStore, logStore, batchStore) = CreateStores();
            var importer = new BatchImporter(blockStore, txStore, receiptStore, logStore, batchStore);

            // Create batch with broken parent hash chain
            var blocks = CreateTestBlocksWithBrokenChain(5);
            var writer = new BatchFileWriter();
            using var stream = new MemoryStream();
            await writer.WriteBatchAsync(stream, _chainId, blocks);

            // Act
            stream.Position = 0;
            var result = await importer.ImportBatchAsync(stream, null, BatchVerificationMode.Quick);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.VerificationResult);
            Assert.False(result.VerificationResult.HeaderChainValid);
            Assert.Contains("Invalid parent hash", result.ErrorMessage);
        }

        [Fact]
        public async Task ImportBatch_WithNoVerification_SkipsValidation()
        {
            // Arrange
            var (blockStore, txStore, receiptStore, logStore, batchStore) = CreateStores();
            var writer = new BatchFileWriter();
            var importer = new BatchImporter(blockStore, txStore, receiptStore, logStore, batchStore);

            var blocks = CreateTestBlocks(3);
            using var stream = new MemoryStream();
            await writer.WriteBatchAsync(stream, _chainId, blocks);

            // Act
            stream.Position = 0;
            var result = await importer.ImportBatchAsync(stream, null, BatchVerificationMode.None);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(3, result.BlocksImported);
        }

        [Fact]
        public async Task ImportBatch_SavesToBatchStore()
        {
            // Arrange
            var (blockStore, txStore, receiptStore, logStore, batchStore) = CreateStores();
            var writer = new BatchFileWriter();
            var importer = new BatchImporter(blockStore, txStore, receiptStore, logStore, batchStore);

            var blocks = CreateTestBlocks(5);
            using var stream = new MemoryStream();
            var batchInfo = await writer.WriteBatchAsync(stream, _chainId, blocks);

            // Act
            stream.Position = 0;
            await importer.ImportBatchAsync(stream, batchInfo.BatchHash, BatchVerificationMode.Quick);

            // Assert
            var storedBatch = await batchStore.GetBatchByHashAsync(batchInfo.BatchHash);
            Assert.NotNull(storedBatch);
            Assert.Equal(BatchStatus.Imported, storedBatch.Status);
            Assert.Equal(0, storedBatch.FromBlock);
            Assert.Equal(4, storedBatch.ToBlock);
        }

        [Fact]
        public async Task ImportBatchFromFile_WithCompression_ImportsSuccessfully()
        {
            // Arrange
            var (blockStore, txStore, receiptStore, logStore, batchStore) = CreateStores();
            var writer = new BatchFileWriter();
            var importer = new BatchImporter(blockStore, txStore, receiptStore, logStore, batchStore);

            var blocks = CreateTestBlocks(5);
            var tempPath = Path.GetTempFileName() + ".bin.gz";

            try
            {
                var batchInfo = await writer.WriteBatchToFileAsync(tempPath, _chainId, blocks, compress: true);

                // Act
                var result = await importer.ImportBatchFromFileAsync(
                    tempPath,
                    batchInfo.BatchHash,
                    BatchVerificationMode.Quick,
                    compressed: true);

                // Assert
                Assert.True(result.Success);
                Assert.Equal(5, result.BlocksImported);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Fact]
        public async Task ImportBatch_WithTransactionsAndReceipts_StoresAll()
        {
            // Arrange
            var (blockStore, txStore, receiptStore, logStore, batchStore) = CreateStores();
            var writer = new BatchFileWriter();
            var importer = new BatchImporter(blockStore, txStore, receiptStore, logStore, batchStore);

            var blocks = CreateTestBlocks(2);
            using var stream = new MemoryStream();
            var batchInfo = await writer.WriteBatchAsync(stream, _chainId, blocks);

            // Act
            stream.Position = 0;
            var result = await importer.ImportBatchAsync(stream, batchInfo.BatchHash, BatchVerificationMode.None);

            // Assert
            Assert.True(result.Success);

            // Verify transactions were stored
            var block0Txs = await txStore.GetByBlockNumberAsync(0);
            Assert.Equal(2, block0Txs.Count);

            var block1Txs = await txStore.GetByBlockNumberAsync(1);
            Assert.Equal(2, block1Txs.Count);

            // Verify receipts were stored
            var block0Receipts = await receiptStore.GetByBlockNumberAsync(0);
            Assert.Equal(2, block0Receipts.Count);
        }

        private (IBlockStore, ITransactionStore, IReceiptStore, ILogStore, IBatchStore) CreateStores()
        {
            var blockStore = new InMemoryBlockStore();
            var txStore = new InMemoryTransactionStore(blockStore);
            var receiptStore = new InMemoryReceiptStore();
            var logStore = new InMemoryLogStore();
            var batchStore = new InMemoryBatchStore();

            return (blockStore, txStore, receiptStore, logStore, batchStore);
        }

        private List<BatchBlockData> CreateTestBlocks(int count)
        {
            var blocks = new List<BatchBlockData>();
            var prevHash = new byte[32];

            for (int i = 0; i < count; i++)
            {
                var (transactions, receipts) = CreateTransactionsAndReceipts(2);
                var transactionsHash = ComputeTransactionsRoot(transactions);
                var header = CreateBlockHeader(i, prevHash, transactionsHash);

                blocks.Add(new BatchBlockData
                {
                    Header = header,
                    Transactions = transactions,
                    Receipts = receipts
                });

                prevHash = _keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(header));
            }

            return blocks;
        }

        private List<BatchBlockData> CreateTestBlocksWithBrokenChain(int count)
        {
            var blocks = new List<BatchBlockData>();
            var prevHash = new byte[32];

            for (int i = 0; i < count; i++)
            {
                // Intentionally use wrong parent hash for block 2
                byte[] wrongHash = new byte[32];
                wrongHash[0] = 0xFF;
                wrongHash[1] = 0xFF;
                var parentHash = (i == 2) ? wrongHash : prevHash;

                var (transactions, receipts) = CreateTransactionsAndReceipts(2);
                var transactionsHash = ComputeTransactionsRoot(transactions);
                var header = CreateBlockHeader(i, parentHash, transactionsHash);

                blocks.Add(new BatchBlockData
                {
                    Header = header,
                    Transactions = transactions,
                    Receipts = receipts
                });

                prevHash = _keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(header));
            }

            return blocks;
        }

        private static readonly RootCalculator _rootCalculator = new RootCalculator();
        private static readonly Sha3Keccack _keccak = new Sha3Keccack();

        private BlockHeader CreateBlockHeader(int blockNumber, byte[] parentHash, byte[] transactionsHash = null)
        {
            return new BlockHeader
            {
                ParentHash = parentHash,
                UnclesHash = _keccak.CalculateHash(RLP.RLP.EncodeList()),
                Coinbase = "0x0000000000000000000000000000000000000001",
                StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                TransactionsHash = transactionsHash ?? DefaultValues.EMPTY_TRIE_HASH,
                ReceiptHash = DefaultValues.EMPTY_TRIE_HASH,
                LogsBloom = new byte[256],
                Difficulty = 1,
                BlockNumber = blockNumber,
                GasLimit = 30000000,
                GasUsed = 42000,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8],
                BaseFee = 1000000000
            };
        }

        private byte[] ComputeTransactionsRoot(List<ISignedTransaction> transactions)
        {
            var encoded = transactions.Select(tx => tx.GetRLPEncoded()).ToList();
            return _rootCalculator.CalculateTransactionsRoot(encoded);
        }

        private (List<ISignedTransaction> transactions, List<Receipt> receipts) CreateTransactionsAndReceipts(int count)
        {
            var transactions = new List<ISignedTransaction>();
            var receipts = new List<Receipt>();
            var key = EthECKey.GenerateKey();
            BigInteger cumulativeGas = 0;

            for (int i = 0; i < count; i++)
            {
                var tx = new Transaction1559(
                    _chainId,
                    nonce: i,
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

                cumulativeGas += 21000;
                receipts.Add(Receipt.CreateStatusReceipt(true, cumulativeGas, new byte[256], new List<Log>()));
            }

            return (transactions, receipts);
        }
    }
}
