using System;
using System.Linq;
using System.Numerics;
using Nethereum.AppChain.Sync;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Signer.Crypto;
using Xunit;

namespace Nethereum.AppChain.Sync.UnitTests
{
    public class BatchFileTests
    {
        private readonly BigInteger _chainId = 420420;

        [Fact]
        public async Task WriteAndReadBatch_RoundTrip_PreservesData()
        {
            // Arrange
            var writer = new BatchFileWriter();
            var reader = new BatchFileReader();
            var blocks = CreateTestBlocks(10);

            using var stream = new MemoryStream();

            // Act - Write
            var batchInfo = await writer.WriteBatchAsync(stream, _chainId, blocks);

            // Assert write results
            Assert.NotNull(batchInfo);
            Assert.Equal(_chainId, batchInfo.ChainId);
            Assert.Equal(0, batchInfo.FromBlock);
            Assert.Equal(9, batchInfo.ToBlock);
            Assert.NotNull(batchInfo.BatchHash);
            Assert.Equal(32, batchInfo.BatchHash.Length);

            // Act - Read header
            stream.Position = 0;
            var header = await reader.ReadHeaderAsync(stream, CancellationToken.None);

            // Assert header
            Assert.Equal((ulong)_chainId, header.ChainId);
            Assert.Equal(0UL, header.FromBlock);
            Assert.Equal(9UL, header.ToBlock);
            Assert.Equal(10, header.BlockCount);

            // Act - Read blocks
            stream.Position = 0;
            var readBlocks = new List<BatchBlock>();
            await foreach (var block in reader.ReadBlocksAsync(stream, CancellationToken.None))
            {
                readBlocks.Add(block);
            }

            // Assert blocks
            Assert.Equal(10, readBlocks.Count);
            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(i, (int)readBlocks[i].Header.BlockNumber);
                Assert.Equal(2, readBlocks[i].TransactionBytes.Count);
                Assert.Equal(2, readBlocks[i].Receipts.Count);
            }
        }

        [Fact]
        public async Task ReadAndVerify_WithCorrectHash_Succeeds()
        {
            // Arrange
            var writer = new BatchFileWriter();
            var reader = new BatchFileReader();
            var blocks = CreateTestBlocks(5);

            using var stream = new MemoryStream();
            var batchInfo = await writer.WriteBatchAsync(stream, _chainId, blocks);

            // Act
            stream.Position = 0;
            var verifiedInfo = await reader.ReadAndVerifyAsync(stream, batchInfo.BatchHash, CancellationToken.None);

            // Assert
            Assert.NotNull(verifiedInfo);
            Assert.Equal(batchInfo.FromBlock, verifiedInfo.FromBlock);
            Assert.Equal(batchInfo.ToBlock, verifiedInfo.ToBlock);
            Assert.Equal(batchInfo.BatchHash, verifiedInfo.BatchHash);
            Assert.Equal(BatchStatus.Verified, verifiedInfo.Status);
        }

        [Fact]
        public async Task ReadAndVerify_WithIncorrectHash_ThrowsException()
        {
            // Arrange
            var writer = new BatchFileWriter();
            var reader = new BatchFileReader();
            var blocks = CreateTestBlocks(5);

            using var stream = new MemoryStream();
            await writer.WriteBatchAsync(stream, _chainId, blocks);

            var wrongHash = new byte[32];
            wrongHash[0] = 0xFF;

            // Act & Assert
            stream.Position = 0;
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                reader.ReadAndVerifyAsync(stream, wrongHash, CancellationToken.None));
        }

        [Fact]
        public async Task WriteToFile_CreatesCompressedFile()
        {
            // Arrange
            var writer = new BatchFileWriter();
            var blocks = CreateTestBlocks(10);
            var tempPath = Path.GetTempFileName() + ".bin.gz";

            try
            {
                // Act
                var batchInfo = await writer.WriteBatchToFileAsync(tempPath, _chainId, blocks, compress: true);

                // Assert
                Assert.True(File.Exists(tempPath));
                Assert.NotNull(batchInfo);

                var fileInfo = new FileInfo(tempPath);
                Assert.True(fileInfo.Length > 0);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Fact]
        public async Task WriteAndReadFromFile_RoundTrip_PreservesData()
        {
            // Arrange
            var writer = new BatchFileWriter();
            var reader = new BatchFileReader();
            var blocks = CreateTestBlocks(5);
            var tempPath = Path.GetTempFileName() + ".bin.gz";

            try
            {
                // Act - Write
                var writeInfo = await writer.WriteBatchToFileAsync(tempPath, _chainId, blocks, compress: true);

                // Act - Read
                var readInfo = await reader.ReadFromFileAsync(tempPath, writeInfo.BatchHash, compressed: true);

                // Assert
                Assert.Equal(writeInfo.FromBlock, readInfo.FromBlock);
                Assert.Equal(writeInfo.ToBlock, readInfo.ToBlock);
                Assert.Equal(writeInfo.BatchHash, readInfo.BatchHash);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        [Fact]
        public void BatchFileFormat_GetBatchFileName_FormatsCorrectly()
        {
            // Act
            var compressedName = BatchFileFormat.GetBatchFileName(420420, 0, 99, compressed: true);
            var uncompressedName = BatchFileFormat.GetBatchFileName(420420, 100, 199, compressed: false);

            // Assert
            Assert.Equal("batch_420420_0_99.bin.zst", compressedName);
            Assert.Equal("batch_420420_100_199.bin", uncompressedName);
        }

        [Fact]
        public void BatchFileFormat_TryParseBatchFileName_ParsesCorrectly()
        {
            // Act
            var success1 = BatchFileFormat.TryParseBatchFileName("batch_420420_0_99.bin.zst", out var chainId1, out var from1, out var to1);
            var success2 = BatchFileFormat.TryParseBatchFileName("batch_1_100_199.bin", out var chainId2, out var from2, out var to2);
            var failure = BatchFileFormat.TryParseBatchFileName("invalid_file.txt", out _, out _, out _);

            // Assert
            Assert.True(success1);
            Assert.Equal(420420, chainId1);
            Assert.Equal(0, from1);
            Assert.Equal(99, to1);

            Assert.True(success2);
            Assert.Equal(1, chainId2);
            Assert.Equal(100, from2);
            Assert.Equal(199, to2);

            Assert.False(failure);
        }

        private List<BatchBlockData> CreateTestBlocks(int count)
        {
            var blocks = new List<BatchBlockData>();
            var prevHash = new byte[32];
            var keccak = new Nethereum.Util.Sha3Keccack();

            for (int i = 0; i < count; i++)
            {
                var (transactions, receipts) = CreateTransactionsAndReceipts(2);
                var transactionsHash = ComputeTransactionsRoot(transactions, keccak);
                var header = CreateBlockHeader(i, prevHash, transactionsHash);

                blocks.Add(new BatchBlockData
                {
                    Header = header,
                    Transactions = transactions,
                    Receipts = receipts
                });

                prevHash = keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(header));
            }

            return blocks;
        }

        private byte[] ComputeTransactionsRoot(List<ISignedTransaction> transactions, Nethereum.Util.Sha3Keccack keccak)
        {
            if (transactions.Count == 0)
                return new byte[32];

            var hashes = transactions.Select(tx => keccak.CalculateHash(tx.GetRLPEncoded())).ToList();

            while (hashes.Count > 1)
            {
                var newHashes = new List<byte[]>();
                for (int i = 0; i < hashes.Count; i += 2)
                {
                    if (i + 1 < hashes.Count)
                    {
                        var combined = new byte[64];
                        Buffer.BlockCopy(hashes[i], 0, combined, 0, 32);
                        Buffer.BlockCopy(hashes[i + 1], 0, combined, 32, 32);
                        newHashes.Add(keccak.CalculateHash(combined));
                    }
                    else
                    {
                        newHashes.Add(hashes[i]);
                    }
                }
                hashes = newHashes;
            }

            return hashes[0];
        }

        private BlockHeader CreateBlockHeader(int blockNumber, byte[] parentHash, byte[] transactionsHash = null)
        {
            return new BlockHeader
            {
                ParentHash = parentHash,
                UnclesHash = new byte[32],
                Coinbase = "0x0000000000000000000000000000000000000001",
                StateRoot = new byte[32],
                TransactionsHash = transactionsHash ?? new byte[32],
                ReceiptHash = new byte[32],
                LogsBloom = new byte[256],
                Difficulty = 1,
                BlockNumber = blockNumber,
                GasLimit = 30000000,
                GasUsed = 21000,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ExtraData = new byte[0],
                MixHash = new byte[32],
                Nonce = new byte[8],
                BaseFee = 1000000000
            };
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
