using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.Model.UnitTests
{
    public class ReceiptEncoderTests
    {
        [Fact]
        public void ShouldEncodeAndDecodeSuccessStatusReceipt()
        {
            var receipt = Receipt.CreateStatusReceipt(
                success: true,
                cumulativeGasUsed: 21000,
                bloom: new byte[256],
                logs: new List<Log>()
            );

            var encoded = ReceiptEncoder.Current.Encode(receipt);
            var decoded = ReceiptEncoder.Current.Decode(encoded);

            Assert.True(decoded.IsStatusReceipt);
            Assert.True(decoded.HasSucceeded);
            Assert.Equal(21000, decoded.CumulativeGasUsed);
            Assert.Empty(decoded.Logs);
        }

        [Fact]
        public void ShouldEncodeAndDecodeFailedStatusReceipt()
        {
            var receipt = Receipt.CreateStatusReceipt(
                success: false,
                cumulativeGasUsed: 50000,
                bloom: new byte[256],
                logs: new List<Log>()
            );

            var encoded = ReceiptEncoder.Current.Encode(receipt);
            var decoded = ReceiptEncoder.Current.Decode(encoded);

            Assert.True(decoded.IsStatusReceipt);
            Assert.False(decoded.HasSucceeded);
            Assert.Equal(50000, decoded.CumulativeGasUsed);
        }

        [Fact]
        public void ShouldEncodeAndDecodePostStateReceipt()
        {
            var stateRoot = "0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray();

            var receipt = Receipt.CreatePostStateReceipt(
                postStateRoot: stateRoot,
                cumulativeGasUsed: 21000,
                bloom: new byte[256],
                logs: new List<Log>()
            );

            var encoded = ReceiptEncoder.Current.Encode(receipt);
            var decoded = ReceiptEncoder.Current.Decode(encoded);

            Assert.False(decoded.IsStatusReceipt);
            Assert.Null(decoded.HasSucceeded);
            Assert.Equal(stateRoot, decoded.PostStateOrStatus);
            Assert.Equal(21000, decoded.CumulativeGasUsed);
        }

        [Fact]
        public void ShouldEncodeAndDecodeReceiptWithLogs()
        {
            var topic = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef".HexToByteArray();
            var log = new Log
            {
                Address = "0xdac17f958d2ee523a2206206994597c13d831ec7",
                Data = "0x0000000000000000000000000000000000000000000000000de0b6b3a7640000".HexToByteArray(),
                Topics = new List<byte[]> { topic }
            };

            var receipt = Receipt.CreateStatusReceipt(
                success: true,
                cumulativeGasUsed: 65000,
                bloom: new byte[256],
                logs: new List<Log> { log }
            );

            var encoded = ReceiptEncoder.Current.Encode(receipt);
            var decoded = ReceiptEncoder.Current.Decode(encoded);

            Assert.Single(decoded.Logs);
            Assert.Equal(log.Address.ToLower(), decoded.Logs[0].Address.ToLower());
            Assert.Equal(log.Data, decoded.Logs[0].Data);
            Assert.Single(decoded.Logs[0].Topics);
            Assert.Equal(topic, decoded.Logs[0].Topics[0]);
        }

        [Fact]
        public void ShouldEncodeAndDecodeTypedReceipt()
        {
            var receipt = Receipt.CreateStatusReceipt(
                success: true,
                cumulativeGasUsed: 21000,
                bloom: new byte[256],
                logs: new List<Log>()
            );

            var encodedType2 = ReceiptEncoder.Current.EncodeTyped(receipt, 0x02);
            Assert.Equal(0x02, encodedType2[0]);

            var decoded = ReceiptEncoder.Current.Decode(encodedType2);
            Assert.True(decoded.HasSucceeded);
            Assert.Equal(21000, decoded.CumulativeGasUsed);
        }

        [Fact]
        public void ShouldEncodeAndDecodeReceiptWithMultipleLogs()
        {
            var topic1 = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef".HexToByteArray();
            var topic2 = "0x8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b925".HexToByteArray();

            var logs = new List<Log>
            {
                new Log
                {
                    Address = "0xdac17f958d2ee523a2206206994597c13d831ec7",
                    Data = new byte[] { 0x01 },
                    Topics = new List<byte[]> { topic1 }
                },
                new Log
                {
                    Address = "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48",
                    Data = new byte[] { 0x02 },
                    Topics = new List<byte[]> { topic2 }
                }
            };

            var receipt = Receipt.CreateStatusReceipt(
                success: true,
                cumulativeGasUsed: 100000,
                bloom: new byte[256],
                logs: logs
            );

            var encoded = ReceiptEncoder.Current.Encode(receipt);
            var decoded = ReceiptEncoder.Current.Decode(encoded);

            Assert.Equal(2, decoded.Logs.Count);
            Assert.Equal(logs[0].Address.ToLower(), decoded.Logs[0].Address.ToLower());
            Assert.Equal(logs[1].Address.ToLower(), decoded.Logs[1].Address.ToLower());
        }

        [Fact]
        public void EncodedReceiptShouldBeRlpList()
        {
            var receipt = Receipt.CreateStatusReceipt(
                success: true,
                cumulativeGasUsed: 21000,
                bloom: new byte[256],
                logs: new List<Log>()
            );

            var encoded = ReceiptEncoder.Current.Encode(receipt);
            Assert.True(encoded[0] >= 0xc0);
        }
    }
}
