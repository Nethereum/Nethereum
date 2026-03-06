using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.AppChain.Sync
{
    public class StateSnapshotWriter : IStateSnapshotWriter
    {
        private readonly IStateStore _stateStore;
        private readonly IBlockStore _blockStore;
        private readonly BigInteger _chainId;
        private readonly Sha3Keccack _keccak = new Sha3Keccack();

        public StateSnapshotWriter(IStateStore stateStore, IBlockStore blockStore, BigInteger chainId)
        {
            _stateStore = stateStore;
            _blockStore = blockStore;
            _chainId = chainId;
        }

        public async Task<StateSnapshotInfo> WriteSnapshotAsync(
            BigInteger blockNumber,
            Stream outputStream,
            CancellationToken cancellationToken = default)
        {
            var block = await _blockStore.GetByNumberAsync(blockNumber);
            if (block == null)
                throw new ArgumentException($"Block {blockNumber} not found", nameof(blockNumber));

            var blockHash = _keccak.CalculateHash(BlockHeaderEncoder.Current.Encode(block));

            var accounts = await _stateStore.GetAllAccountsAsync();
            var codeHashSet = new HashSet<string>();
            var codes = new List<(byte[] hash, byte[] code)>();

            long storageSlotCount = 0;
            using var hashStream = new MemoryStream();
            using var writer = new BinaryWriter(outputStream, System.Text.Encoding.UTF8, leaveOpen: true);

            writer.Write(BatchFileFormat.STATE_MAGIC);
            writer.Write(BatchFileFormat.STATE_VERSION);
            writer.Write((long)_chainId);
            writer.Write((long)blockNumber);
            writer.Write(block.StateRoot ?? new byte[32]);
            writer.Write((long)accounts.Count);

            var codePlaceholderPosition = outputStream.Position;
            writer.Write((long)0);

            foreach (var kvp in accounts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var address = kvp.Key;
                var account = kvp.Value;

                WriteString(writer, address);
                WriteBytes(writer, account.Nonce.ToByteArray());
                WriteBytes(writer, account.Balance.ToByteArray());
                WriteBytes(writer, account.CodeHash ?? new byte[32]);
                WriteBytes(writer, account.StateRoot ?? new byte[32]);

                hashStream.Write(System.Text.Encoding.UTF8.GetBytes(address));
                hashStream.Write(account.Nonce.ToByteArray());
                hashStream.Write(account.Balance.ToByteArray());

                var storage = await _stateStore.GetAllStorageAsync(address);
                writer.Write(storage.Count);

                foreach (var slot in storage)
                {
                    WriteBytes(writer, slot.Key.ToByteArray());
                    WriteBytes(writer, slot.Value);
                    hashStream.Write(slot.Key.ToByteArray());
                    hashStream.Write(slot.Value);
                    storageSlotCount++;
                }

                if (account.CodeHash != null && account.CodeHash.Length > 0)
                {
                    var codeHashKey = account.CodeHash.ToHex();
                    if (!codeHashSet.Contains(codeHashKey) && !IsEmptyCodeHash(account.CodeHash))
                    {
                        codeHashSet.Add(codeHashKey);
                        var code = await _stateStore.GetCodeAsync(account.CodeHash);
                        if (code != null)
                        {
                            codes.Add((account.CodeHash, code));
                        }
                    }
                }
            }

            writer.Write(codes.Count);
            foreach (var (codeHash, code) in codes)
            {
                WriteBytes(writer, codeHash);
                WriteBytes(writer, code);
                hashStream.Write(codeHash);
                hashStream.Write(code);
            }

            var currentPosition = outputStream.Position;
            outputStream.Position = codePlaceholderPosition;
            writer.Write((long)codes.Count);
            outputStream.Position = currentPosition;

            await outputStream.FlushAsync(cancellationToken);

            var snapshotHash = _keccak.CalculateHash(hashStream.ToArray());

            return new StateSnapshotInfo
            {
                ChainId = _chainId,
                BlockNumber = blockNumber,
                BlockHash = blockHash,
                StateRoot = block.StateRoot ?? Array.Empty<byte>(),
                SnapshotHash = snapshotHash,
                AccountCount = accounts.Count,
                StorageSlotCount = storageSlotCount,
                CodeCount = codes.Count,
                TotalSizeBytes = outputStream.Length,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
        }

        public async Task<StateSnapshotInfo> WriteSnapshotToFileAsync(
            BigInteger blockNumber,
            string filePath,
            bool compress = true,
            CancellationToken cancellationToken = default)
        {
            var tempPath = filePath + ".tmp";
            StateSnapshotInfo snapshotInfo;

            try
            {
                if (compress)
                {
                    using var memoryStream = new MemoryStream();
                    snapshotInfo = await WriteSnapshotAsync(blockNumber, memoryStream, cancellationToken);

                    memoryStream.Position = 0;
                    using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
                    using var gzipStream = new System.IO.Compression.GZipStream(fileStream, System.IO.Compression.CompressionLevel.Optimal);
                    await memoryStream.CopyToAsync(gzipStream, cancellationToken);
                }
                else
                {
                    using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
                    snapshotInfo = await WriteSnapshotAsync(blockNumber, fileStream, cancellationToken);
                }

                if (File.Exists(filePath))
                    File.Delete(filePath);
                File.Move(tempPath, filePath);

                return snapshotInfo;
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
            data ??= Array.Empty<byte>();
            writer.Write(data.Length);
            writer.Write(data);
        }

        private void WriteString(BinaryWriter writer, string value)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static readonly byte[] EmptyCodeHash = new byte[]
        {
            0xc5, 0xd2, 0x46, 0x01, 0x86, 0xf7, 0x23, 0x3c,
            0x92, 0x7e, 0x7d, 0xb2, 0xdc, 0xc7, 0x03, 0xc0,
            0xe5, 0x00, 0xb6, 0x53, 0xca, 0x82, 0x27, 0x3b,
            0x7b, 0xfa, 0xd8, 0x04, 0x5d, 0x85, 0xa4, 0x70
        };

        private static bool IsEmptyCodeHash(byte[] hash)
        {
            if (hash == null || hash.Length != 32) return false;
            for (int i = 0; i < 32; i++)
            {
                if (hash[i] != EmptyCodeHash[i]) return false;
            }
            return true;
        }
    }
}
