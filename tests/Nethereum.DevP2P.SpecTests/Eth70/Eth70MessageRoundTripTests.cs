using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Eth70
{
    public class Eth70MessageRoundTripTests
    {
        private static byte[] Make32(byte fill)
        {
            var bytes = new byte[32];
            for (int i = 0; i < 32; i++) bytes[i] = (byte)(fill ^ i);
            return bytes;
        }

        private static Receipt MakeReceipt(byte statusSeed, int logDataLength, byte txType = 0)
        {
            var log = Log.Create(new byte[logDataLength], "0x" + new string('0', 40));
            return new Receipt
            {
                TransactionType = txType,
                PostStateOrStatus = new byte[] { statusSeed },
                CumulativeGasUsed = new EvmUInt256(21000UL + statusSeed),
                Bloom = new byte[256],
                Logs = new List<Log> { log }
            };
        }


        [Fact]
        public void GetReceiptsMessage70_RoundTrips_ReqId_FirstIndex_Hashes()
        {
            var msg = new GetReceiptsMessage70
            {
                RequestId = 0xC0FFEEul,
                FirstBlockReceiptIndex = 7ul,
                BlockHashes = new[] { Make32(0x11), Make32(0x22) }
            };

            var bytes = GetReceiptsMessage70Encoder.Encode(msg);
            var decoded = GetReceiptsMessage70Encoder.Decode(bytes);

            Assert.Equal(msg.RequestId, decoded.RequestId);
            Assert.Equal(msg.FirstBlockReceiptIndex, decoded.FirstBlockReceiptIndex);
            Assert.Equal(msg.BlockHashes.Length, decoded.BlockHashes.Length);
            for (int i = 0; i < msg.BlockHashes.Length; i++)
                Assert.Equal(msg.BlockHashes[i].ToHex(), decoded.BlockHashes[i].ToHex());
        }

        [Fact]
        public void GetReceiptsMessage70_RoundTrips_Twin_MutatedFirstIndexNotEqual()
        {
            var original = new GetReceiptsMessage70
            {
                RequestId = 1ul,
                FirstBlockReceiptIndex = 3ul,
                BlockHashes = new[] { Make32(0xAA) }
            };
            var mutated = new GetReceiptsMessage70
            {
                RequestId = 1ul,
                FirstBlockReceiptIndex = 99ul,
                BlockHashes = new[] { Make32(0xAA) }
            };

            var decodedOriginal = GetReceiptsMessage70Encoder.Decode(GetReceiptsMessage70Encoder.Encode(original));
            var decodedMutated = GetReceiptsMessage70Encoder.Decode(GetReceiptsMessage70Encoder.Encode(mutated));

            Assert.NotEqual(decodedOriginal.FirstBlockReceiptIndex, decodedMutated.FirstBlockReceiptIndex);
            Assert.NotEqual(
                GetReceiptsMessage70Encoder.Encode(original).ToHex(),
                GetReceiptsMessage70Encoder.Encode(mutated).ToHex());
        }

        [Fact]
        public void GetReceiptsMessage70_FirstIndexZero_RoundTrips()
        {
            var msg = new GetReceiptsMessage70
            {
                RequestId = 1ul,
                FirstBlockReceiptIndex = 0ul,
                BlockHashes = new[] { Make32(0x01) }
            };

            var decoded = GetReceiptsMessage70Encoder.Decode(GetReceiptsMessage70Encoder.Encode(msg));

            Assert.Equal(0ul, decoded.FirstBlockReceiptIndex);
        }


        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ReceiptsMessage70_RoundTrips_IncompleteFlag_ReceiptLists(bool lastBlockIncomplete)
        {
            var block0 = new List<Receipt> { MakeReceipt(1, 8), MakeReceipt(2, 4) };
            var block1 = new List<Receipt> { MakeReceipt(3, 0) };

            var msg = new ReceiptsMessage70
            {
                RequestId = 42ul,
                LastBlockIncomplete = lastBlockIncomplete,
                ReceiptsByBlock = new List<List<Receipt>> { block0, block1 }
            };

            var bytes = ReceiptsMessageEth70Encoder.Encode(msg);
            var decoded = ReceiptsMessageEth70Encoder.Decode(bytes);

            Assert.Equal(msg.RequestId, decoded.RequestId);
            Assert.Equal(lastBlockIncomplete, decoded.LastBlockIncomplete);
            Assert.Equal(2, decoded.ReceiptsByBlock.Count);
            Assert.Equal(2, decoded.ReceiptsByBlock[0].Count);
            Assert.Equal(1, decoded.ReceiptsByBlock[1].Count);

            for (int b = 0; b < msg.ReceiptsByBlock.Count; b++)
            {
                for (int r = 0; r < msg.ReceiptsByBlock[b].Count; r++)
                {
                    Assert.Equal(msg.ReceiptsByBlock[b][r].PostStateOrStatus, decoded.ReceiptsByBlock[b][r].PostStateOrStatus);
                    Assert.Equal(msg.ReceiptsByBlock[b][r].CumulativeGasUsed, decoded.ReceiptsByBlock[b][r].CumulativeGasUsed);
                    Assert.Equal(msg.ReceiptsByBlock[b][r].Logs.Count, decoded.ReceiptsByBlock[b][r].Logs.Count);
                }
            }
        }

        [Fact]
        public void ReceiptsMessage70_IncompleteFlag_Twin_IsLoadBearingOnTheWire()
        {
            var receipts = new List<List<Receipt>> { new List<Receipt> { MakeReceipt(1, 4) } };
            var complete = new ReceiptsMessage70 { RequestId = 5ul, LastBlockIncomplete = false, ReceiptsByBlock = receipts };
            var incomplete = new ReceiptsMessage70 { RequestId = 5ul, LastBlockIncomplete = true, ReceiptsByBlock = receipts };

            var completeBytes = ReceiptsMessageEth70Encoder.Encode(complete);
            var incompleteBytes = ReceiptsMessageEth70Encoder.Encode(incomplete);

            Assert.NotEqual(completeBytes.ToHex(), incompleteBytes.ToHex());
            Assert.False(ReceiptsMessageEth70Encoder.Decode(completeBytes).LastBlockIncomplete);
            Assert.True(ReceiptsMessageEth70Encoder.Decode(incompleteBytes).LastBlockIncomplete);
        }

        [Fact]
        public void ReceiptsMessage70_Empty_RoundTrips()
        {
            var msg = new ReceiptsMessage70 { RequestId = 1ul, LastBlockIncomplete = false };
            var decoded = ReceiptsMessageEth70Encoder.Decode(ReceiptsMessageEth70Encoder.Encode(msg));
            Assert.Empty(decoded.ReceiptsByBlock);
            Assert.False(decoded.LastBlockIncomplete);
        }


        [Fact]
        public void Receipts70_ReceiptBody_MatchesEth69ReceiptListBytes()
        {
            var blockReceipts = new List<Receipt> { MakeReceipt(1, 16), MakeReceipt(2, 0), MakeReceipt(3, 32, txType: 2) };

            var directEth69Bytes = ReceiptsMessageEth69Encoder.EncodeBlockReceipts(blockReceipts);

            var msg70 = new ReceiptsMessage70
            {
                RequestId = 9ul,
                LastBlockIncomplete = false,
                ReceiptsByBlock = new List<List<Receipt>> { blockReceipts }
            };
            var messageBytes = ReceiptsMessageEth70Encoder.Encode(msg70);

            Assert.Contains(directEth69Bytes.ToHex(), messageBytes.ToHex());
        }
    }
}
