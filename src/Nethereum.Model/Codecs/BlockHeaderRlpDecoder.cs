using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.Model.Codecs
{
    internal static class BlockHeaderRlpDecoder
    {
        private const int HashBytes = 32;
        private const int AddressBytes = 20;
        private const int BloomBytes = 256;
        private const int NonceBytes = 8;
        private const int MaxScalarBytes = 32;

        public static BlockHeader Decode(byte[] raw)
        {
            return Decode(raw, 0, null);
        }

        public static BlockHeader Decode(byte[] raw, int requiredFieldCount, string shape)
        {
            var reader = new RlpReader(raw);
            var header = Read(reader, requiredFieldCount, shape);
            reader.EnsureEnd();
            return header;
        }

        public static BlockHeader Read(RlpReader reader)
        {
            return Read(reader, 0, null);
        }

        private static BlockHeader Read(RlpReader reader, int requiredFieldCount, string shape)
        {
            if (reader.PeekKind() != RlpKind.List)
                throw new ArgumentException("Block header RLP is not a list.", "rawdata");

            var scope = reader.ReadListHeader();
            var count = reader.CountItems(scope);
            if (requiredFieldCount != 0 && count != requiredFieldCount)
                throw new InvalidOperationException(
                    $"{shape} header codec expects {requiredFieldCount} fields, got {count}");
            if (BlockHeaderCodecSelector.ForFieldCount(count) == null)
                throw new ArgumentException(
                    $"A block header with {count} fields matches no fork this build knows. " +
                    "Decoding it as the nearest shorter fork would discard the trailing fields, and the " +
                    "header would then re-encode to different bytes and fail its own hash check.",
                    "rawdata");

            var header = new BlockHeader
            {
                ParentHash = Fixed(reader, HashBytes, "parentHash"),
                UnclesHash = Fixed(reader, HashBytes, "unclesHash"),
                Coinbase = Fixed(reader, AddressBytes, "coinbase").ToHex(),
                StateRoot = Fixed(reader, HashBytes, "stateRoot"),
                TransactionsHash = Fixed(reader, HashBytes, "transactionsRoot"),
                ReceiptHash = Fixed(reader, HashBytes, "receiptsRoot"),
                LogsBloom = Fixed(reader, BloomBytes, "logsBloom"),
                Difficulty = Scalar(reader, "difficulty"),
                BlockNumber = Scalar(reader, "number"),
                GasLimit = unchecked((long)reader.ReadUInt64()),
                GasUsed = unchecked((long)reader.ReadUInt64()),
                Timestamp = unchecked((long)reader.ReadUInt64())
            };

            var extraData = reader.ReadBytes();
            header.ExtraData = extraData.Length == 0 ? null : extraData;
            header.MixHash = Fixed(reader, HashBytes, "mixHash");
            header.Nonce = Fixed(reader, NonceBytes, "nonce");

            if (count >= 16) header.BaseFee = Scalar(reader, "baseFeePerGas");
            if (count >= 17) header.WithdrawalsRoot = Fixed(reader, HashBytes, "withdrawalsRoot");
            if (count >= 20)
            {
                header.BlobGasUsed = unchecked((long)reader.ReadUInt64());
                header.ExcessBlobGas = unchecked((long)reader.ReadUInt64());
                header.ParentBeaconBlockRoot = Fixed(reader, HashBytes, "parentBeaconBlockRoot");
            }
            if (count >= 21) header.RequestsHash = Fixed(reader, HashBytes, "requestsHash");
            if (count >= 23)
            {
                header.BlockAccessListHash = Fixed(reader, HashBytes, "blockAccessListHash");
                header.SlotNumber = reader.ReadUInt64();
            }

            reader.EnsureEnd(scope);
            return header;
        }

        private static byte[] Fixed(RlpReader reader, int width, string field)
        {
            var size = reader.PeekKind() == RlpKind.List ? -1 : reader.PeekSize();
            if (size != width)
                throw new InvalidOperationException(
                    $"RLP_INVALID_FIELD_WIDTH: header field '{field}' must be {width} bytes, got {(size < 0 ? "a list" : size.ToString())}.");
            return reader.ReadFixed(width);
        }

        private static EvmUInt256 Scalar(RlpReader reader, string field)
        {
            var bytes = reader.ReadBytes();
            if (!RlpScalar.IsCanonical(bytes))
                throw new InvalidOperationException($"RLP_LEADING_ZEROS: header field '{field}' is encoded with a leading zero byte.");
            if (bytes.Length > MaxScalarBytes)
                throw new InvalidOperationException($"RLP_INVALID_FIELD_OVERFLOW_256: header field '{field}' is {bytes.Length} bytes.");
            return bytes.ToEvmUInt256FromRLPDecoded();
        }
    }
}
