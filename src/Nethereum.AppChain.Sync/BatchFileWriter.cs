using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.AppChain.Sync
{
    public class BatchFileWriter : IBatchWriter
    {
        private readonly Sha3Keccack _keccak = new Sha3Keccack();

        public async Task<BatchInfo> WriteBatchAsync(
            Stream outputStream,
            BigInteger chainId,
            IEnumerable<BatchBlockData> blocks,
            CancellationToken cancellationToken = default)
        {
            var blockList = new List<BatchBlockData>(blocks);
            if (blockList.Count == 0)
                throw new ArgumentException("No blocks provided", nameof(blocks));

            var fromBlock = blockList[0].Header.BlockNumber;
            var toBlock = blockList[blockList.Count - 1].Header.BlockNumber;

            using var hashStream = new MemoryStream();
            using var writer = new BinaryWriter(outputStream, System.Text.Encoding.UTF8, leaveOpen: true);

            writer.Write(BatchFileFormat.MAGIC);
            writer.Write(BatchFileFormat.VERSION);
            writer.Write((long)chainId);
            writer.Write((long)fromBlock);
            writer.Write((long)toBlock);
            writer.Write((ushort)blockList.Count);

            byte[] lastStateRoot = null;

            foreach (var block in blockList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var headerBytes = BlockHeaderEncoder.Current.Encode(block.Header);
                WriteBytes(writer, headerBytes);
                hashStream.Write(headerBytes, 0, headerBytes.Length);

                writer.Write((ushort)block.Transactions.Count);
                foreach (var tx in block.Transactions)
                {
                    var txBytes = tx.GetRLPEncoded();
                    WriteBytes(writer, txBytes);
                    hashStream.Write(txBytes, 0, txBytes.Length);
                }

                writer.Write((ushort)block.Receipts.Count);
                foreach (var receipt in block.Receipts)
                {
                    var receiptBytes = ReceiptEncoder.Current.Encode(receipt);
                    WriteBytes(writer, receiptBytes);
                    hashStream.Write(receiptBytes, 0, receiptBytes.Length);
                }

                lastStateRoot = block.Header.StateRoot;
            }

            await outputStream.FlushAsync(cancellationToken);

            var batchHash = _keccak.CalculateHash(hashStream.ToArray());

            return new BatchInfo
            {
                ChainId = chainId,
                FromBlock = fromBlock,
                ToBlock = toBlock,
                BatchHash = batchHash,
                ToBlockStateRoot = lastStateRoot,
                Status = BatchStatus.Pending
            };
        }

        public async Task<BatchInfo> WriteBatchToFileAsync(
            string filePath,
            BigInteger chainId,
            IEnumerable<BatchBlockData> blocks,
            bool compress = true,
            CancellationToken cancellationToken = default)
        {
            var tempPath = filePath + ".tmp";
            BatchInfo batchInfo;

            try
            {
                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                {
                    Stream outputStream = fileStream;

                    if (compress)
                    {
                        outputStream = CreateCompressionStream(fileStream);
                    }

                    try
                    {
                        batchInfo = await WriteBatchAsync(outputStream, chainId, blocks, cancellationToken);
                    }
                    finally
                    {
                        if (compress && outputStream != fileStream)
                        {
                            await outputStream.DisposeAsync();
                        }
                    }
                }

                if (File.Exists(filePath))
                    File.Delete(filePath);
                File.Move(tempPath, filePath);

                return batchInfo;
            }
            catch
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
                throw;
            }
        }

        private void WriteBytes(BinaryWriter writer, byte[] data)
        {
            writer.Write(data.Length);
            writer.Write(data);
        }

        private Stream CreateCompressionStream(Stream outputStream)
        {
            return new System.IO.Compression.GZipStream(outputStream, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true);
        }
    }

    public class BatchBlockData
    {
        public BlockHeader Header { get; set; }
        public List<ISignedTransaction> Transactions { get; set; } = new List<ISignedTransaction>();
        public List<Receipt> Receipts { get; set; } = new List<Receipt>();
    }
}
