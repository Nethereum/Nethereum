using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Util;

namespace Nethereum.AppChain.Sync
{
    public class StateSnapshotReader : IStateSnapshotReader
    {
        private readonly Sha3Keccack _keccak = new Sha3Keccack();

        public async Task<StateSnapshotHeader> ReadHeaderAsync(Stream inputStream, CancellationToken cancellationToken = default)
        {
            using var reader = new BinaryReader(inputStream, System.Text.Encoding.UTF8, leaveOpen: true);

            var magic = reader.ReadBytes(4);
            if (!ByteUtil.AreEqual(magic, BatchFileFormat.STATE_MAGIC))
                throw new InvalidDataException("Invalid state snapshot magic bytes");

            var version = reader.ReadUInt16();
            if (version > BatchFileFormat.STATE_VERSION)
                throw new InvalidDataException($"Unsupported state snapshot version: {version}");

            var chainId = reader.ReadInt64();
            var blockNumber = reader.ReadInt64();
            var stateRoot = reader.ReadBytes(32);
            var accountCount = reader.ReadInt64();
            var codeCount = reader.ReadInt64();

            return new StateSnapshotHeader
            {
                Version = version,
                ChainId = (ulong)chainId,
                BlockNumber = (ulong)blockNumber,
                StateRoot = stateRoot,
                AccountCount = (ulong)accountCount,
                CodeCount = (ulong)codeCount
            };
        }

        public async IAsyncEnumerable<StateAccount> ReadAccountsAsync(
            Stream inputStream,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var reader = new BinaryReader(inputStream, System.Text.Encoding.UTF8, leaveOpen: true);

            var magic = reader.ReadBytes(4);
            if (!ByteUtil.AreEqual(magic, BatchFileFormat.STATE_MAGIC))
                throw new InvalidDataException("Invalid state snapshot magic bytes");

            var version = reader.ReadUInt16();
            var chainId = reader.ReadInt64();
            var blockNumber = reader.ReadInt64();
            var stateRoot = reader.ReadBytes(32);
            var accountCount = reader.ReadInt64();
            var codeCount = reader.ReadInt64();

            for (long i = 0; i < accountCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var address = ReadString(reader);
                var nonce = new BigInteger(ReadBytes(reader), isUnsigned: true);
                var balance = new BigInteger(ReadBytes(reader), isUnsigned: true);
                var codeHash = ReadBytes(reader);
                var storageRoot = ReadBytes(reader);

                var storageCount = reader.ReadInt32();
                for (int s = 0; s < storageCount; s++)
                {
                    ReadBytes(reader);
                    ReadBytes(reader);
                }

                yield return new StateAccount
                {
                    Address = address,
                    Nonce = nonce,
                    Balance = balance,
                    CodeHash = codeHash,
                    StorageRoot = storageRoot
                };
            }
        }

        public async IAsyncEnumerable<StateStorageSlot> ReadStorageSlotsAsync(
            Stream inputStream,
            string address,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var reader = new BinaryReader(inputStream, System.Text.Encoding.UTF8, leaveOpen: true);

            var magic = reader.ReadBytes(4);
            if (!ByteUtil.AreEqual(magic, BatchFileFormat.STATE_MAGIC))
                throw new InvalidDataException("Invalid state snapshot magic bytes");

            var version = reader.ReadUInt16();
            var chainId = reader.ReadInt64();
            var blockNumber = reader.ReadInt64();
            var stateRoot = reader.ReadBytes(32);
            var accountCount = reader.ReadInt64();
            var codeCount = reader.ReadInt64();

            for (long i = 0; i < accountCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var accountAddress = ReadString(reader);
                ReadBytes(reader);
                ReadBytes(reader);
                ReadBytes(reader);
                ReadBytes(reader);

                var storageCount = reader.ReadInt32();

                if (AddressUtil.Current.AreAddressesTheSame(accountAddress, address))
                {
                    for (int s = 0; s < storageCount; s++)
                    {
                        var slot = new BigInteger(ReadBytes(reader), isUnsigned: true);
                        var value = ReadBytes(reader);

                        yield return new StateStorageSlot
                        {
                            Address = address,
                            Slot = slot,
                            Value = value
                        };
                    }
                    yield break;
                }
                else
                {
                    for (int s = 0; s < storageCount; s++)
                    {
                        ReadBytes(reader);
                        ReadBytes(reader);
                    }
                }
            }
        }

        public async IAsyncEnumerable<StateCode> ReadCodesAsync(
            Stream inputStream,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var reader = new BinaryReader(inputStream, System.Text.Encoding.UTF8, leaveOpen: true);

            var magic = reader.ReadBytes(4);
            if (!ByteUtil.AreEqual(magic, BatchFileFormat.STATE_MAGIC))
                throw new InvalidDataException("Invalid state snapshot magic bytes");

            var version = reader.ReadUInt16();
            var chainId = reader.ReadInt64();
            var blockNumber = reader.ReadInt64();
            var stateRoot = reader.ReadBytes(32);
            var accountCount = reader.ReadInt64();
            var codeCount = reader.ReadInt64();

            for (long i = 0; i < accountCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ReadString(reader);
                ReadBytes(reader);
                ReadBytes(reader);
                ReadBytes(reader);
                ReadBytes(reader);

                var storageCount = reader.ReadInt32();
                for (int s = 0; s < storageCount; s++)
                {
                    ReadBytes(reader);
                    ReadBytes(reader);
                }
            }

            var actualCodeCount = reader.ReadInt32();
            for (int c = 0; c < actualCodeCount; c++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var codeHash = ReadBytes(reader);
                var code = ReadBytes(reader);

                yield return new StateCode
                {
                    CodeHash = codeHash,
                    Code = code
                };
            }
        }

        public async Task<StateSnapshotInfo> ReadAndVerifyAsync(
            Stream inputStream,
            byte[] expectedStateRoot,
            CancellationToken cancellationToken = default)
        {
            using var hashStream = new MemoryStream();
            using var reader = new BinaryReader(inputStream, System.Text.Encoding.UTF8, leaveOpen: true);

            var magic = reader.ReadBytes(4);
            if (!ByteUtil.AreEqual(magic, BatchFileFormat.STATE_MAGIC))
                throw new InvalidDataException("Invalid state snapshot magic bytes");

            var version = reader.ReadUInt16();
            var chainId = reader.ReadInt64();
            var blockNumber = reader.ReadInt64();
            var stateRoot = reader.ReadBytes(32);
            var accountCount = reader.ReadInt64();
            var codeCount = reader.ReadInt64();

            if (expectedStateRoot != null && !ByteUtil.AreEqual(stateRoot, expectedStateRoot))
            {
                throw new InvalidDataException("State root mismatch");
            }

            long storageSlotCount = 0;

            for (long i = 0; i < accountCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var address = ReadString(reader);
                var nonce = ReadBytes(reader);
                var balance = ReadBytes(reader);
                var accountCodeHash = ReadBytes(reader);
                var accountStorageRoot = ReadBytes(reader);

                hashStream.Write(System.Text.Encoding.UTF8.GetBytes(address));
                hashStream.Write(nonce);
                hashStream.Write(balance);

                var storageCount = reader.ReadInt32();
                for (int s = 0; s < storageCount; s++)
                {
                    var slot = ReadBytes(reader);
                    var value = ReadBytes(reader);
                    hashStream.Write(slot);
                    hashStream.Write(value);
                    storageSlotCount++;
                }
            }

            var actualCodeCount = reader.ReadInt32();
            for (int c = 0; c < actualCodeCount; c++)
            {
                var codeHash = ReadBytes(reader);
                var code = ReadBytes(reader);
                hashStream.Write(codeHash);
                hashStream.Write(code);
            }

            var snapshotHash = _keccak.CalculateHash(hashStream.ToArray());

            return new StateSnapshotInfo
            {
                ChainId = chainId,
                BlockNumber = blockNumber,
                StateRoot = stateRoot,
                SnapshotHash = snapshotHash,
                AccountCount = accountCount,
                StorageSlotCount = storageSlotCount,
                CodeCount = actualCodeCount,
                TotalSizeBytes = inputStream.Position
            };
        }

        private byte[] ReadBytes(BinaryReader reader)
        {
            var length = reader.ReadInt32();
            return reader.ReadBytes(length);
        }

        private string ReadString(BinaryReader reader)
        {
            var length = reader.ReadInt32();
            var bytes = reader.ReadBytes(length);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

    }
}
