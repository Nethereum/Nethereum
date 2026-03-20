using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Ssz;
using Nethereum.Util;

namespace Nethereum.Model.SSZ
{
    public class SszBlockHeaderEncoder
    {
        public static readonly bool[] ActiveFields =
        {
            true, true, true, true, true, true, true, true, true,
            true, true, true, true, true, true, true, true
        };

        private static readonly bool[] GasAmountsActiveFields = { true, true };
        private static readonly bool[] BlobFeesActiveFields = { true, true };

        public const int MaxExtraDataBytes = 32;
        public const int AddressLength = 20;

        public static readonly SszBlockHeaderEncoder Current = new SszBlockHeaderEncoder();

        // ================================================================
        // Encode
        // ================================================================

        public byte[] Encode(BlockHeader header)
        {
            // EIP-7807 ProgressiveContainer — serialized identically to Container
            // Fixed: parent_hash(32) + miner(20) + state_root(32) + tx_hash(32) + receipt_hash(32) +
            //   number(8) + gas_limits(16) + gas_used(16) + timestamp(8) + offset_extra(4) +
            //   mix_hash(32) + base_fees(64) + withdrawals_root(32) + excess_gas(16) +
            //   parent_beacon_root(32) + requests_hash(32) + system_logs_root(32)
            // Variable: extra_data

            // GasAmounts: 2x uint64 = 16 bytes fixed
            // BlobFeesPerGas: 2x uint256 = 64 bytes fixed
            var fixedSize = 32 + 20 + 32 + 32 + 32 + 8 + 16 + 16 + 8 + 4 + 32 + 64 + 32 + 16 + 32 + 32 + 32;
            var extraData = header.ExtraData ?? Array.Empty<byte>();

            using var writer = new SszWriter();
            writer.WriteFixedBytes(header.ParentHash ?? new byte[32], 32);
            writer.WriteFixedBytes(GetAddressBytes(header.Coinbase), AddressLength);
            writer.WriteFixedBytes(header.StateRoot ?? new byte[32], 32);
            writer.WriteFixedBytes(header.TransactionsHash ?? new byte[32], 32);
            writer.WriteFixedBytes(header.ReceiptHash ?? new byte[32], 32);
            writer.WriteUInt64((ulong)header.BlockNumber);

            // GasAmounts: gas_limits
            writer.WriteUInt64((ulong)header.GasLimit);
            writer.WriteUInt64(header.BlobGasUsed.HasValue ? (ulong)header.BlobGasUsed.Value : 0UL);

            // GasAmounts: gas_used
            writer.WriteUInt64((ulong)header.GasUsed);
            writer.WriteUInt64(0UL); // blob gas used in gas_used — not tracked separately in BlockHeader

            writer.WriteUInt64((ulong)header.Timestamp);
            writer.WriteUInt32((uint)fixedSize); // offset to extra_data

            writer.WriteFixedBytes(header.MixHash ?? new byte[32], 32);

            // BlobFeesPerGas: base_fees_per_gas (regular + blob)
            writer.WriteFixedBytes((header.BaseFee ?? BigInteger.Zero).BigIntegerToFixedLengthByteArrayLE(32), 32);
            writer.WriteFixedBytes(BigInteger.Zero.BigIntegerToFixedLengthByteArrayLE(32), 32); // blob base fee — not in BlockHeader yet

            writer.WriteFixedBytes(header.WithdrawalsRoot ?? new byte[32], 32);

            // GasAmounts: excess_gas
            writer.WriteUInt64(0UL); // regular excess
            writer.WriteUInt64(header.ExcessBlobGas.HasValue ? (ulong)header.ExcessBlobGas.Value : 0UL);

            writer.WriteFixedBytes(header.ParentBeaconBlockRoot ?? new byte[32], 32);
            writer.WriteFixedBytes(header.RequestsHash ?? new byte[32], 32);
            writer.WriteFixedBytes(new byte[32], 32); // system_logs_root — not in BlockHeader yet

            // Variable: extra_data
            writer.WriteBytes(extraData);

            return writer.ToArray();
        }

        // ================================================================
        // Decode
        // ================================================================

        public BlockHeader Decode(ReadOnlySpan<byte> data)
        {
            var reader = new SszReader(data);

            var parentHash = reader.ReadFixedBytes(32);
            var miner = reader.ReadFixedBytes(AddressLength);
            var stateRoot = reader.ReadFixedBytes(32);
            var txHash = reader.ReadFixedBytes(32);
            var receiptHash = reader.ReadFixedBytes(32);
            var number = reader.ReadUInt64();

            // GasAmounts: gas_limits
            var gasLimit = reader.ReadUInt64();
            var gasLimitBlob = reader.ReadUInt64();

            // GasAmounts: gas_used
            var gasUsed = reader.ReadUInt64();
            var gasUsedBlob = reader.ReadUInt64();

            var timestamp = reader.ReadUInt64();
            var extraDataOffset = reader.ReadUInt32();

            var mixHash = reader.ReadFixedBytes(32);

            // BlobFeesPerGas
            var baseFeeBytes = reader.ReadFixedBytes(32);
            var blobBaseFeeBytes = reader.ReadFixedBytes(32);

            var withdrawalsRoot = reader.ReadFixedBytes(32);

            // GasAmounts: excess_gas
            var excessRegular = reader.ReadUInt64();
            var excessBlobGas = reader.ReadUInt64();

            var parentBeaconRoot = reader.ReadFixedBytes(32);
            var requestsHash = reader.ReadFixedBytes(32);
            var systemLogsRoot = reader.ReadFixedBytes(32);

            // Variable: extra_data
            var extraData = data.Length > (int)extraDataOffset
                ? data.Slice((int)extraDataOffset).ToArray()
                : Array.Empty<byte>();

            return new BlockHeader
            {
                ParentHash = parentHash,
                Coinbase = "0x" + miner.ToHex(),
                StateRoot = stateRoot,
                TransactionsHash = txHash,
                ReceiptHash = receiptHash,
                BlockNumber = (long)number,
                GasLimit = (long)gasLimit,
                GasUsed = (long)gasUsed,
                Timestamp = (long)timestamp,
                MixHash = mixHash,
                BaseFee = baseFeeBytes.ToBigIntegerFromFixedLengthByteArrayLE(),
                WithdrawalsRoot = withdrawalsRoot,
                BlobGasUsed = gasLimitBlob > 0 ? (long?)gasLimitBlob : null,
                ExcessBlobGas = excessBlobGas > 0 ? (long?)excessBlobGas : null,
                ParentBeaconBlockRoot = parentBeaconRoot,
                RequestsHash = requestsHash,
                ExtraData = extraData
            };
        }

        // ================================================================
        // HashTreeRoot
        // ================================================================

        public byte[] HashTreeRoot(BlockHeader header)
        {
            var fieldRoots = new List<byte[]>
            {
                SszHashTreeRootHelper.HashTreeRootBytes32(header.ParentHash),
                SszHashTreeRootHelper.HashTreeRootAddress(header.Coinbase),
                SszHashTreeRootHelper.HashTreeRootBytes32(header.StateRoot),
                SszHashTreeRootHelper.HashTreeRootBytes32(header.TransactionsHash),
                SszHashTreeRootHelper.HashTreeRootBytes32(header.ReceiptHash),
                SszHashTreeRootHelper.HashTreeRootUint64((ulong)header.BlockNumber),
                HashTreeRootGasAmounts((ulong)header.GasLimit,
                    header.BlobGasUsed.HasValue ? (ulong)header.BlobGasUsed.Value : 0UL),
                HashTreeRootGasAmounts((ulong)header.GasUsed, 0UL),
                SszHashTreeRootHelper.HashTreeRootUint64((ulong)header.Timestamp),
                SszHashTreeRootHelper.HashTreeRootByteList(header.ExtraData, MaxExtraDataBytes),
                SszHashTreeRootHelper.HashTreeRootBytes32(header.MixHash),
                HashTreeRootBlobFees(header.BaseFee, null),
                SszHashTreeRootHelper.HashTreeRootBytes32(header.WithdrawalsRoot ?? new byte[32]),
                HashTreeRootGasAmounts(0UL,
                    header.ExcessBlobGas.HasValue ? (ulong)header.ExcessBlobGas.Value : 0UL),
                SszHashTreeRootHelper.HashTreeRootBytes32(header.ParentBeaconBlockRoot ?? new byte[32]),
                SszHashTreeRootHelper.HashTreeRootBytes32(header.RequestsHash ?? new byte[32]),
                SszHashTreeRootHelper.HashTreeRootBytes32(new byte[32])
            };

            return SszMerkleizer.HashTreeRootProgressiveContainer(fieldRoots, ActiveFields);
        }

        public byte[] BlockHash(BlockHeader header)
        {
            return HashTreeRoot(header);
        }

        private byte[] HashTreeRootGasAmounts(ulong regular, ulong blob)
        {
            var fieldRoots = new List<byte[]>
            {
                SszHashTreeRootHelper.HashTreeRootUint64(regular),
                SszHashTreeRootHelper.HashTreeRootUint64(blob)
            };
            return SszMerkleizer.HashTreeRootProgressiveContainer(fieldRoots, GasAmountsActiveFields);
        }

        private byte[] HashTreeRootBlobFees(BigInteger? regularFee, BigInteger? blobFee)
        {
            var fieldRoots = new List<byte[]>
            {
                SszHashTreeRootHelper.HashTreeRootUint256(regularFee),
                SszHashTreeRootHelper.HashTreeRootUint256(blobFee)
            };
            return SszMerkleizer.HashTreeRootProgressiveContainer(fieldRoots, BlobFeesActiveFields);
        }

        private static byte[] GetAddressBytes(string address)
        {
            if (string.IsNullOrEmpty(address)) return new byte[AddressLength];
            return address.HexToByteArray();
        }
    }
}
