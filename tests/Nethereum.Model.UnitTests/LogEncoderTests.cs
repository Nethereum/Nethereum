using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.Model.UnitTests
{
    public class LogEncoderTests
    {
        [Fact]
        public void ShouldEncodeAndDecodeEmptyLog()
        {
            var log = new Log
            {
                Address = "0x0000000000000000000000000000000000000000",
                Data = null,
                Topics = new List<byte[]>()
            };

            var encoded = LogEncoder.Current.Encode(log);
            var decoded = LogEncoder.Current.Decode(encoded);

            Assert.Equal(log.Address.ToLower(), decoded.Address.ToLower());
            Assert.Empty(decoded.Topics);
        }

        [Fact]
        public void ShouldEncodeAndDecodeLogWithTopics()
        {
            var topic1 = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef".HexToByteArray();
            var topic2 = "0x000000000000000000000000742d35cc6634c0532925a3b844bc9e7595f0ab00".HexToByteArray();
            var topic3 = "0x00000000000000000000000088e6a0c2ddd26feeb64f039a2c41296fcb3f5640".HexToByteArray();

            var log = new Log
            {
                Address = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48",
                Data = "0x0000000000000000000000000000000000000000000000000de0b6b3a7640000".HexToByteArray(),
                Topics = new List<byte[]> { topic1, topic2, topic3 }
            };

            var encoded = LogEncoder.Current.Encode(log);
            var decoded = LogEncoder.Current.Decode(encoded);

            Assert.Equal(log.Address.ToLower(), decoded.Address.ToLower());
            Assert.Equal(log.Data, decoded.Data);
            Assert.Equal(3, decoded.Topics.Count);
            Assert.Equal(topic1, decoded.Topics[0]);
            Assert.Equal(topic2, decoded.Topics[1]);
            Assert.Equal(topic3, decoded.Topics[2]);
        }

        [Fact]
        public void ShouldEncodeAndDecodeLogWithSingleTopic()
        {
            var topic = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef".HexToByteArray();

            var log = new Log
            {
                Address = "0xdac17f958d2ee523a2206206994597c13d831ec7",
                Data = new byte[0],
                Topics = new List<byte[]> { topic }
            };

            var encoded = LogEncoder.Current.Encode(log);
            var decoded = LogEncoder.Current.Decode(encoded);

            Assert.Equal(log.Address.ToLower(), decoded.Address.ToLower());
            Assert.Single(decoded.Topics);
            Assert.Equal(topic, decoded.Topics[0]);
        }

        [Fact]
        public void EncodedLogShouldBeRlpList()
        {
            var log = new Log
            {
                Address = "0x0000000000000000000000000000000000000001",
                Data = new byte[] { 0x01, 0x02, 0x03 },
                Topics = new List<byte[]>()
            };

            var encoded = LogEncoder.Current.Encode(log);
            Assert.True(encoded[0] >= 0xc0);
        }
    }
}
