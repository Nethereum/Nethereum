using System;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.SSZ;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Model.SSZ.Tests
{
    public class SszBlockHeaderRoundTripTests
    {
        private static BlockHeader MakeHeader(
            long number = 12345678,
            long gasLimit = 30000000,
            long gasUsed = 21000,
            long timestamp = 1700000000)
        {
            return new BlockHeader
            {
                ParentHash = "0xabcdef0000000000000000000000000000000000000000000000000000000001".HexToByteArray(),
                Coinbase = "0xdead000000000000000000000000000000000001",
                StateRoot = "0x1111111111111111111111111111111111111111111111111111111111111111".HexToByteArray(),
                TransactionsHash = "0x2222222222222222222222222222222222222222222222222222222222222222".HexToByteArray(),
                ReceiptHash = "0x3333333333333333333333333333333333333333333333333333333333333333".HexToByteArray(),
                BlockNumber = number,
                GasLimit = gasLimit,
                GasUsed = gasUsed,
                Timestamp = timestamp,
                MixHash = "0x4444444444444444444444444444444444444444444444444444444444444444".HexToByteArray(),
                BaseFee = 30000000000,
                WithdrawalsRoot = "0x5555555555555555555555555555555555555555555555555555555555555555".HexToByteArray(),
                ParentBeaconBlockRoot = "0x6666666666666666666666666666666666666666666666666666666666666666".HexToByteArray(),
                RequestsHash = "0x7777777777777777777777777777777777777777777777777777777777777777".HexToByteArray(),
                ExtraData = new byte[] { 0x01, 0x02, 0x03 }
            };
        }

        [Fact]
        public void BasicHeader_RoundTrip()
        {
            var original = MakeHeader();
            var encoded = SszBlockHeaderEncoder.Current.Encode(original);
            var decoded = SszBlockHeaderEncoder.Current.Decode(encoded);

            Assert.Equal(original.ParentHash, decoded.ParentHash);
            Assert.Equal(original.Coinbase.ToLower(), decoded.Coinbase.ToLower());
            Assert.Equal(original.StateRoot, decoded.StateRoot);
            Assert.Equal(original.TransactionsHash, decoded.TransactionsHash);
            Assert.Equal(original.ReceiptHash, decoded.ReceiptHash);
            Assert.Equal(original.BlockNumber, decoded.BlockNumber);
            Assert.Equal(original.GasLimit, decoded.GasLimit);
            Assert.Equal(original.GasUsed, decoded.GasUsed);
            Assert.Equal(original.Timestamp, decoded.Timestamp);
            Assert.Equal(original.MixHash, decoded.MixHash);
            Assert.Equal(original.BaseFee, decoded.BaseFee);
            Assert.Equal(original.WithdrawalsRoot, decoded.WithdrawalsRoot);
            Assert.Equal(original.ParentBeaconBlockRoot, decoded.ParentBeaconBlockRoot);
            Assert.Equal(original.RequestsHash, decoded.RequestsHash);
            Assert.Equal(original.ExtraData, decoded.ExtraData);
        }

        [Fact]
        public void EmptyHeader_RoundTrip()
        {
            var original = new BlockHeader
            {
                ParentHash = new byte[32],
                Coinbase = "0x0000000000000000000000000000000000000000",
                StateRoot = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                MixHash = new byte[32]
            };

            var encoded = SszBlockHeaderEncoder.Current.Encode(original);
            var decoded = SszBlockHeaderEncoder.Current.Decode(encoded);

            Assert.Equal(0L, decoded.BlockNumber);
            Assert.Equal(0L, decoded.GasLimit);
            Assert.Equal(0L, decoded.GasUsed);
            Assert.Equal(0L, decoded.Timestamp);
            Assert.Equal((EvmUInt256?)EvmUInt256.Zero, decoded.BaseFee);
            Assert.Empty(decoded.ExtraData);
        }

        [Fact]
        public void WithBlobGas_RoundTrip()
        {
            var original = MakeHeader();
            original.BlobGasUsed = 131072; // 1 blob
            original.ExcessBlobGas = 786432;

            var encoded = SszBlockHeaderEncoder.Current.Encode(original);
            var decoded = SszBlockHeaderEncoder.Current.Decode(encoded);

            Assert.Equal(original.BlobGasUsed, decoded.BlobGasUsed);
            Assert.Equal(original.ExcessBlobGas, decoded.ExcessBlobGas);
        }

        [Fact]
        public void ExtraData_Empty_RoundTrip()
        {
            var original = MakeHeader();
            original.ExtraData = Array.Empty<byte>();

            var encoded = SszBlockHeaderEncoder.Current.Encode(original);
            var decoded = SszBlockHeaderEncoder.Current.Decode(encoded);

            Assert.Empty(decoded.ExtraData);
        }

        [Fact]
        public void ExtraData_MaxLength_RoundTrip()
        {
            var original = MakeHeader();
            original.ExtraData = new byte[SszBlockHeaderEncoder.MaxExtraDataBytes];
            new Random(42).NextBytes(original.ExtraData);

            var encoded = SszBlockHeaderEncoder.Current.Encode(original);
            var decoded = SszBlockHeaderEncoder.Current.Decode(encoded);

            Assert.Equal(original.ExtraData, decoded.ExtraData);
        }

        [Fact]
        public void LargeBaseFee_RoundTrip()
        {
            var original = MakeHeader();
            original.BaseFee = (EvmUInt256)100 * (EvmUInt256)1_000_000_000_000_000_000UL; // 100 ETH in wei (1e20)

            var encoded = SszBlockHeaderEncoder.Current.Encode(original);
            var decoded = SszBlockHeaderEncoder.Current.Decode(encoded);

            Assert.Equal(original.BaseFee, decoded.BaseFee);
        }

        [Fact]
        public void HashTreeRoot_StableAcrossEncodeDecode()
        {
            var original = MakeHeader();

            var rootBefore = SszBlockHeaderEncoder.Current.HashTreeRoot(original);
            var encoded = SszBlockHeaderEncoder.Current.Encode(original);
            var decoded = SszBlockHeaderEncoder.Current.Decode(encoded);
            var rootAfter = SszBlockHeaderEncoder.Current.HashTreeRoot(decoded);

            Assert.Equal(rootBefore, rootAfter);
        }

        [Fact]
        public void BlockHash_StableAcrossEncodeDecode()
        {
            var original = MakeHeader();
            original.BlobGasUsed = 131072;
            original.ExcessBlobGas = 786432;

            var hashBefore = SszBlockHeaderEncoder.Current.BlockHash(original);
            var encoded = SszBlockHeaderEncoder.Current.Encode(original);
            var decoded = SszBlockHeaderEncoder.Current.Decode(encoded);
            var hashAfter = SszBlockHeaderEncoder.Current.BlockHash(decoded);

            Assert.Equal(hashBefore, hashAfter);
        }

        [Fact]
        public void FullE2E_TransactionsAndReceipts_LinkedToHeader()
        {
            // Build transactions
            var tx = new Transaction1559(1, 42, 2000000000, 30000000000, 21000,
                "0xbeef000000000000000000000000000000000001", 1000000000000000000,
                "", new List<AccessListItem>());

            var txRoot = SszTransactionEncoder.Current.HashTreeRootTransaction1559(tx);
            var txsRoot = SszTransactionEncoder.Current.HashTreeRootTransactionsRoot(
                new List<byte[]> { txRoot });

            // Build receipt
            var log = Log.Create(new byte[32],
                "0xbeef000000000000000000000000000000000001",
                "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef".HexToByteArray());
            var receiptDataRoot = SszReceiptEncoder.Current.HashTreeRootBasicReceipt(
                "0xdead000000000000000000000000000000000001", 21000, new List<Log> { log }, true);
            var receiptRoot = SszReceiptEncoder.Current.HashTreeRootReceipt(
                SszReceiptEncoder.SelectorBasicReceipt, receiptDataRoot);
            var receiptsRoot = SszReceiptEncoder.Current.HashTreeRootReceiptsRoot(
                new List<byte[]> { receiptRoot });

            // Build header with those roots
            var header = MakeHeader();
            header.TransactionsHash = txsRoot;
            header.ReceiptHash = receiptsRoot;

            // Encode → Decode → Verify hash stability
            var hashBefore = SszBlockHeaderEncoder.Current.BlockHash(header);
            var encoded = SszBlockHeaderEncoder.Current.Encode(header);
            var decoded = SszBlockHeaderEncoder.Current.Decode(encoded);
            var hashAfter = SszBlockHeaderEncoder.Current.BlockHash(decoded);

            Assert.Equal(hashBefore, hashAfter);
            Assert.Equal(32, hashBefore.Length);

            // Verify the decoded header still links to the correct roots
            Assert.Equal(txsRoot, decoded.TransactionsHash);
            Assert.Equal(receiptsRoot, decoded.ReceiptHash);
        }
    }
}
