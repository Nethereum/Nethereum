using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.AppChain.Sync
{
    public class BatchFileReader : IBatchReader
    {
        private readonly Sha3Keccack _keccak = new Sha3Keccack();

        public async Task<BatchHeader> ReadHeaderAsync(Stream inputStream, CancellationToken cancellationToken = default)
        {
            using var reader = new BinaryReader(inputStream, System.Text.Encoding.UTF8, leaveOpen: true);

            var magic = reader.ReadBytes(4);
            if (!ByteUtil.AreEqual(magic, BatchFileFormat.MAGIC))
                throw new InvalidDataException("Invalid batch file magic bytes");

            var version = reader.ReadUInt16();
            if (version > BatchFileFormat.VERSION)
                throw new InvalidDataException($"Unsupported batch version: {version}");

            var chainId = reader.ReadInt64();
            var fromBlock = reader.ReadInt64();
            var toBlock = reader.ReadInt64();
            var blockCount = reader.ReadUInt16();

            return new BatchHeader
            {
                Version = version,
                ChainId = (ulong)chainId,
                FromBlock = (ulong)fromBlock,
                ToBlock = (ulong)toBlock,
                BlockCount = blockCount
            };
        }

        public async IAsyncEnumerable<BatchBlock> ReadBlocksAsync(
            Stream inputStream,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var reader = new BinaryReader(inputStream, System.Text.Encoding.UTF8, leaveOpen: true);

            var magic = reader.ReadBytes(4);
            if (!ByteUtil.AreEqual(magic, BatchFileFormat.MAGIC))
                throw new InvalidDataException("Invalid batch file magic bytes");

            var version = reader.ReadUInt16();
            var chainId = reader.ReadInt64();
            var fromBlock = reader.ReadInt64();
            var toBlock = reader.ReadInt64();
            var blockCount = reader.ReadUInt16();

            for (int i = 0; i < blockCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var headerBytes = ReadBytes(reader);
                var header = BlockHeaderEncoder.Current.Decode(headerBytes);

                var txCount = reader.ReadUInt16();
                var transactions = new List<byte[]>(txCount);
                for (int t = 0; t < txCount; t++)
                {
                    transactions.Add(ReadBytes(reader));
                }

                var receiptCount = reader.ReadUInt16();
                var receipts = new List<Receipt>(receiptCount);
                for (int r = 0; r < receiptCount; r++)
                {
                    var receiptBytes = ReadBytes(reader);
                    receipts.Add(ReceiptEncoder.Current.Decode(receiptBytes));
                }

                yield return new BatchBlock
                {
                    Header = header,
                    TransactionBytes = transactions,
                    Receipts = receipts
                };
            }
        }

        public async Task<BatchInfo> ReadAndVerifyAsync(
            Stream inputStream,
            byte[] expectedBatchHash,
            CancellationToken cancellationToken = default)
        {
            using var hashStream = new MemoryStream();
            using var reader = new BinaryReader(inputStream, System.Text.Encoding.UTF8, leaveOpen: true);

            var magic = reader.ReadBytes(4);
            if (!ByteUtil.AreEqual(magic, BatchFileFormat.MAGIC))
                throw new InvalidDataException("Invalid batch file magic bytes");

            var version = reader.ReadUInt16();
            var chainId = reader.ReadInt64();
            var fromBlock = reader.ReadInt64();
            var toBlock = reader.ReadInt64();
            var blockCount = reader.ReadUInt16();

            byte[] lastStateRoot = null;

            for (int i = 0; i < blockCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var headerBytes = ReadBytes(reader);
                hashStream.Write(headerBytes, 0, headerBytes.Length);
                var header = BlockHeaderEncoder.Current.Decode(headerBytes);
                lastStateRoot = header.StateRoot;

                var txCount = reader.ReadUInt16();
                for (int t = 0; t < txCount; t++)
                {
                    var txBytes = ReadBytes(reader);
                    hashStream.Write(txBytes, 0, txBytes.Length);
                }

                var receiptCount = reader.ReadUInt16();
                for (int r = 0; r < receiptCount; r++)
                {
                    var receiptBytes = ReadBytes(reader);
                    hashStream.Write(receiptBytes, 0, receiptBytes.Length);
                }
            }

            var computedHash = _keccak.CalculateHash(hashStream.ToArray());

            if (expectedBatchHash != null && !ByteUtil.AreEqual(computedHash, expectedBatchHash))
            {
                throw new InvalidDataException("Batch hash verification failed");
            }

            return new BatchInfo
            {
                ChainId = chainId,
                FromBlock = fromBlock,
                ToBlock = toBlock,
                BatchHash = computedHash,
                ToBlockStateRoot = lastStateRoot,
                Status = BatchStatus.Verified
            };
        }

        public async Task<BatchInfo> ReadFromFileAsync(
            string filePath,
            byte[] expectedBatchHash = null,
            bool compressed = true,
            CancellationToken cancellationToken = default)
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
            Stream inputStream = fileStream;

            if (compressed)
            {
                inputStream = new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionMode.Decompress, leaveOpen: true);
            }

            try
            {
                return await ReadAndVerifyAsync(inputStream, expectedBatchHash, cancellationToken);
            }
            finally
            {
                if (compressed && inputStream != fileStream)
                {
                    await inputStream.DisposeAsync();
                }
            }
        }

        private byte[] ReadBytes(BinaryReader reader)
        {
            var length = reader.ReadInt32();
            return reader.ReadBytes(length);
        }

    }
}
