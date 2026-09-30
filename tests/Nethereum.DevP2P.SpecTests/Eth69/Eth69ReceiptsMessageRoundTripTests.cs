using System.Collections.Generic;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Eth69
{
    public class Eth69ReceiptsMessageRoundTripTests
    {
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
        public void ReceiptsMessage_RoundTrips_MultiBlock_MultiTxType()
        {
            var block0 = new List<Receipt> { MakeReceipt(1, 8), MakeReceipt(2, 4, txType: 2) };
            var block1 = new List<Receipt> { MakeReceipt(3, 0, txType: 1) };
            var msg = new ReceiptsMessage
            {
                RequestId = 0xABCDEFul,
                ReceiptsByBlock = new List<List<Receipt>> { block0, block1 }
            };

            var decoded = ReceiptsMessageEth69Encoder.Decode(ReceiptsMessageEth69Encoder.Encode(msg));

            Assert.Equal(msg.RequestId, decoded.RequestId);
            Assert.Equal(2, decoded.ReceiptsByBlock.Count);
            Assert.Equal(2, decoded.ReceiptsByBlock[0].Count);
            Assert.Equal(1, decoded.ReceiptsByBlock[1].Count);
            for (int b = 0; b < msg.ReceiptsByBlock.Count; b++)
                for (int r = 0; r < msg.ReceiptsByBlock[b].Count; r++)
                {
                    var expected = msg.ReceiptsByBlock[b][r];
                    var actual = decoded.ReceiptsByBlock[b][r];
                    Assert.Equal(expected.TransactionType, actual.TransactionType);
                    Assert.Equal(expected.PostStateOrStatus, actual.PostStateOrStatus);
                    Assert.Equal(expected.CumulativeGasUsed, actual.CumulativeGasUsed);
                    Assert.Equal(expected.Logs.Count, actual.Logs.Count);
                }
        }

        [Fact]
        public void ReceiptsMessage_Decode_RecomputesBloom_NotCarriedOnWire()
        {
            var receipt = MakeReceipt(1, 8);
            for (int i = 0; i < receipt.Bloom.Length; i++) receipt.Bloom[i] = 0xFF;

            var msg = new ReceiptsMessage
            {
                RequestId = 1ul,
                ReceiptsByBlock = new List<List<Receipt>> { new List<Receipt> { receipt } }
            };

            var decoded = ReceiptsMessageEth69Encoder.Decode(ReceiptsMessageEth69Encoder.Encode(msg));
            var decodedBloom = decoded.ReceiptsByBlock[0][0].Bloom;

            Assert.Equal(256, decodedBloom.Length);
            Assert.NotEqual(receipt.Bloom, decodedBloom);
        }

        [Fact]
        public void ReceiptsMessage_Empty_RoundTrips()
        {
            var msg = new ReceiptsMessage { RequestId = 7ul };
            var decoded = ReceiptsMessageEth69Encoder.Decode(ReceiptsMessageEth69Encoder.Encode(msg));
            Assert.Equal(7ul, decoded.RequestId);
            Assert.Empty(decoded.ReceiptsByBlock);
        }
    }
}
