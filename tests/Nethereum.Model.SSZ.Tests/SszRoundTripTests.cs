using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.SSZ;
using Xunit;

namespace Nethereum.Model.SSZ.Tests
{
    public class SszLogRoundTripTests
    {
        [Fact]
        public void EmptyLog_RoundTrip()
        {
            var original = new Log
            {
                Address = "0x0000000000000000000000000000000000000000",
                Topics = new List<byte[]>(),
                Data = Array.Empty<byte>()
            };

            var encoded = SszLogEncoder.Current.Encode(original);
            var decoded = SszLogEncoder.Current.Decode(encoded);

            Assert.Equal(original.Address, decoded.Address);
            Assert.Empty(decoded.Topics);
            Assert.Empty(decoded.Data);
        }

        [Fact]
        public void LogWithTopicsAndData_RoundTrip()
        {
            var transferTopic = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef".HexToByteArray();
            var fromTopic = new byte[32];
            fromTopic[31] = 0x01;
            var toTopic = new byte[32];
            toTopic[31] = 0x02;

            var original = new Log
            {
                Address = "0xdAC17F958D2ee523a2206206994597C13D831ec7",
                Topics = new List<byte[]> { transferTopic, fromTopic, toTopic },
                Data = "0x0000000000000000000000000000000000000000000000000de0b6b3a7640000".HexToByteArray()
            };

            var encoded = SszLogEncoder.Current.Encode(original);
            var decoded = SszLogEncoder.Current.Decode(encoded);

            Assert.Equal(original.Address.ToLower(), decoded.Address.ToLower());
            Assert.Equal(3, decoded.Topics.Count);
            Assert.Equal(transferTopic, decoded.Topics[0]);
            Assert.Equal(fromTopic, decoded.Topics[1]);
            Assert.Equal(toTopic, decoded.Topics[2]);
            Assert.Equal(original.Data, decoded.Data);
        }

        [Fact]
        public void Log_MaxTopics_RoundTrip()
        {
            var original = new Log
            {
                Address = "0xdead000000000000000000000000000000000001",
                Topics = new List<byte[]>
                {
                    new byte[32], new byte[32], new byte[32], new byte[32] // 4 topics = max
                },
                Data = new byte[] { 0x01, 0x02, 0x03 }
            };

            var encoded = SszLogEncoder.Current.Encode(original);
            var decoded = SszLogEncoder.Current.Decode(encoded);

            Assert.Equal(4, decoded.Topics.Count);
            Assert.Equal(original.Data, decoded.Data);
        }

        [Fact]
        public void Log_HashTreeRoot_StableAcrossEncodeDecode()
        {
            var original = Log.Create(
                "0xabcdef".HexToByteArray(),
                "0xdead000000000000000000000000000000000001",
                "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef".HexToByteArray());

            var rootBefore = SszLogEncoder.Current.HashTreeRoot(original);
            var encoded = SszLogEncoder.Current.Encode(original);
            var decoded = SszLogEncoder.Current.Decode(encoded);
            var rootAfter = SszLogEncoder.Current.HashTreeRoot(decoded);

            Assert.Equal(rootBefore, rootAfter);
        }

        [Fact]
        public void Log_LargeData_RoundTrip()
        {
            var data = new byte[1024];
            new Random(42).NextBytes(data);

            var original = new Log
            {
                Address = "0xbeef000000000000000000000000000000000002",
                Topics = new List<byte[]>
                {
                    "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef".HexToByteArray()
                },
                Data = data
            };

            var encoded = SszLogEncoder.Current.Encode(original);
            var decoded = SszLogEncoder.Current.Decode(encoded);

            Assert.Equal(original.Data, decoded.Data);
            Assert.Equal(SszLogEncoder.Current.HashTreeRoot(original),
                SszLogEncoder.Current.HashTreeRoot(decoded));
        }
    }

    public class SszReceiptRoundTripTests
    {
        private const string TestFrom = "0xdead000000000000000000000000000000000001";
        private const string TestContract = "0x1234567890abcdef1234567890abcdef12345678";

        [Fact]
        public void BasicReceipt_Empty_RoundTrip()
        {
            var encoded = SszReceiptEncoder.Current.EncodeBasicReceipt(
                TestFrom, 21000, new List<Log>(), true);
            SszReceiptEncoder.Current.DecodeBasicReceipt(encoded,
                out var from, out var gasUsed, out var logs, out var status);

            Assert.Equal(TestFrom.ToLower(), from.ToLower());
            Assert.Equal(21000UL, gasUsed);
            Assert.Empty(logs);
            Assert.True(status);
        }

        [Fact]
        public void BasicReceipt_StatusFalse_RoundTrip()
        {
            var encoded = SszReceiptEncoder.Current.EncodeBasicReceipt(
                TestFrom, 42000, new List<Log>(), false);
            SszReceiptEncoder.Current.DecodeBasicReceipt(encoded,
                out var from, out var gasUsed, out var logs, out var status);

            Assert.Equal(42000UL, gasUsed);
            Assert.False(status);
        }

        [Fact]
        public void BasicReceipt_WithLogs_RoundTrip()
        {
            var log = Log.Create(
                "0x0000000000000000000000000000000000000000000000000de0b6b3a7640000".HexToByteArray(),
                "0xdAC17F958D2ee523a2206206994597C13D831ec7",
                "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef".HexToByteArray());

            var encoded = SszReceiptEncoder.Current.EncodeBasicReceipt(
                TestFrom, 53000, new List<Log> { log }, true);
            SszReceiptEncoder.Current.DecodeBasicReceipt(encoded,
                out var from, out var gasUsed, out var logs, out var status);

            Assert.Equal(53000UL, gasUsed);
            Assert.True(status);
            Assert.Single(logs);
            Assert.Equal(log.Address.ToLower(), logs[0].Address.ToLower());
            Assert.Single(logs[0].Topics);
            Assert.Equal(log.Data, logs[0].Data);
        }

        [Fact]
        public void BasicReceipt_MultipleLogs_RoundTrip()
        {
            var log1 = Log.Create(new byte[] { 0x01 }, "0xdead000000000000000000000000000000000001");
            var log2 = Log.Create(new byte[] { 0x02 },
                "0xbeef000000000000000000000000000000000002",
                "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef".HexToByteArray());

            var encoded = SszReceiptEncoder.Current.EncodeBasicReceipt(
                TestFrom, 100000, new List<Log> { log1, log2 }, true);
            SszReceiptEncoder.Current.DecodeBasicReceipt(encoded,
                out _, out _, out var logs, out _);

            Assert.Equal(2, logs.Count);
            Assert.Equal(log1.Data, logs[0].Data);
            Assert.Equal(log2.Data, logs[1].Data);
            Assert.Single(logs[1].Topics);
        }

        [Fact]
        public void BasicReceipt_HashTreeRoot_StableAcrossEncodeDecode()
        {
            var log = Log.Create(new byte[32], TestFrom);
            var rootBefore = SszReceiptEncoder.Current.HashTreeRootBasicReceipt(
                TestFrom, 21000, new List<Log> { log }, true);

            var encoded = SszReceiptEncoder.Current.EncodeBasicReceipt(
                TestFrom, 21000, new List<Log> { log }, true);
            SszReceiptEncoder.Current.DecodeBasicReceipt(encoded,
                out var from, out var gasUsed, out var logs, out var status);
            var rootAfter = SszReceiptEncoder.Current.HashTreeRootBasicReceipt(
                from, gasUsed, logs, status);

            Assert.Equal(rootBefore, rootAfter);
        }

        [Fact]
        public void CreateReceipt_RoundTrip()
        {
            var encoded = SszReceiptEncoder.Current.EncodeCreateReceipt(
                TestFrom, 500000, TestContract, new List<Log>(), true);
            SszReceiptEncoder.Current.DecodeCreateReceipt(encoded,
                out var from, out var gasUsed, out var contractAddress,
                out var logs, out var status);

            Assert.Equal(TestFrom.ToLower(), from.ToLower());
            Assert.Equal(500000UL, gasUsed);
            Assert.Equal(TestContract.ToLower(), contractAddress.ToLower());
            Assert.Empty(logs);
            Assert.True(status);
        }

        [Fact]
        public void CreateReceipt_HashTreeRoot_StableAcrossEncodeDecode()
        {
            var rootBefore = SszReceiptEncoder.Current.HashTreeRootCreateReceipt(
                TestFrom, 500000, TestContract, new List<Log>(), true);

            var encoded = SszReceiptEncoder.Current.EncodeCreateReceipt(
                TestFrom, 500000, TestContract, new List<Log>(), true);
            SszReceiptEncoder.Current.DecodeCreateReceipt(encoded,
                out var from, out var gasUsed, out var contractAddress,
                out var logs, out var status);
            var rootAfter = SszReceiptEncoder.Current.HashTreeRootCreateReceipt(
                from, gasUsed, contractAddress, logs, status);

            Assert.Equal(rootBefore, rootAfter);
        }

        [Fact]
        public void Receipt_UnionWrapper_RoundTrip()
        {
            var innerData = SszReceiptEncoder.Current.EncodeBasicReceipt(
                TestFrom, 21000, new List<Log>(), true);

            var encoded = SszReceiptEncoder.Current.EncodeReceipt(
                SszReceiptEncoder.SelectorBasicReceipt, innerData);

            var selector = SszReceiptEncoder.Current.DecodeReceiptSelector(encoded);
            var data = SszReceiptEncoder.Current.DecodeReceiptData(encoded);

            Assert.Equal(SszReceiptEncoder.SelectorBasicReceipt, selector);
            Assert.Equal(innerData.Length, data.Length);
            Assert.True(data.ToArray().SequenceEqual(innerData));
        }

        [Fact]
        public void Receipt_FullE2E_EncodeDecodeHashVerify()
        {
            var log = Log.Create(
                "0x0000000000000000000000000000000000000000000000000de0b6b3a7640000".HexToByteArray(),
                "0xdAC17F958D2ee523a2206206994597C13D831ec7",
                "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef".HexToByteArray());

            // 1. Compute hash_tree_root from original data
            var originalRoot = SszReceiptEncoder.Current.HashTreeRootBasicReceipt(
                TestFrom, 53000, new List<Log> { log }, true);

            // 2. Encode to SSZ bytes
            var innerEncoded = SszReceiptEncoder.Current.EncodeBasicReceipt(
                TestFrom, 53000, new List<Log> { log }, true);
            var receiptEncoded = SszReceiptEncoder.Current.EncodeReceipt(
                SszReceiptEncoder.SelectorBasicReceipt, innerEncoded);

            // 3. Decode back
            var selector = SszReceiptEncoder.Current.DecodeReceiptSelector(receiptEncoded);
            var data = SszReceiptEncoder.Current.DecodeReceiptData(receiptEncoded);
            Assert.Equal(SszReceiptEncoder.SelectorBasicReceipt, selector);

            SszReceiptEncoder.Current.DecodeBasicReceipt(data,
                out var from, out var gasUsed, out var logs, out var status);

            // 4. Verify decoded values
            Assert.Equal(TestFrom.ToLower(), from.ToLower());
            Assert.Equal(53000UL, gasUsed);
            Assert.True(status);
            Assert.Single(logs);

            // 5. Recompute hash_tree_root and verify it matches
            var recomputedRoot = SszReceiptEncoder.Current.HashTreeRootBasicReceipt(
                from, gasUsed, logs, status);
            Assert.Equal(originalRoot, recomputedRoot);

            // 6. Wrap in union and verify
            var unionRoot = SszReceiptEncoder.Current.HashTreeRootReceipt(selector, recomputedRoot);
            Assert.Equal(32, unionRoot.Length);
        }
    }
}
