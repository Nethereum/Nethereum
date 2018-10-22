using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System;
using System.Numerics;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
    public class EventTopicDecoderTests
    {
        [Event("Transfer")]
        public class TransferEvent
        {
            [Parameter("address", "_from", 1, true)]
            public string From {get; set;}

            [Parameter("address", "_to", 2, true)]
            public string To {get; set;}

            [Parameter("uint256", "_value", 3, true)]
            public BigInteger Value {get; set;}
        }

        [Event("Transfer")]
        public class TransferEventMissingIndexedTopic
        {
            [Parameter("address", "_from", 1, true)]
            public string From {get; set;}

            [Parameter("address", "_to", 2, true)]
            public string To {get; set;}

            [Parameter("uint256", "_value", 3, false)]
            public BigInteger Value {get; set;}
        }

        [Fact]
        public void DecodeTopics_Decodes_Transfer_Event()
        {
            var topics = new[]
            {
                "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef",
                "0x0000000000000000000000000000000000000000000000000000000000000000",
                "0x000000000000000000000000c14934679e71ef4d18b6ae927fe2b953c7fd9b91",
                "0x0000000000000000000000000000000000000000000000400000402000000001"
            };

            var data = "0x";

            var transferDto = new TransferEvent();
            new EventTopicDecoder().DecodeTopics(transferDto, topics, data);

            Assert.Equal("0x0000000000000000000000000000000000000000", transferDto.From);
            Assert.Equal("0xc14934679e71ef4d18b6ae927fe2b953c7fd9b91", transferDto.To);
            Assert.Equal("1180591691223594434561", transferDto.Value.ToString());
        }

        [Fact]
        public void DecodeTopics_Validates_Number_Of_Indexed_Topics()
        {
            var topics = new[]
            {
                "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef",
                "0x0000000000000000000000000000000000000000000000000000000000000000",
                "0x000000000000000000000000c14934679e71ef4d18b6ae927fe2b953c7fd9b91",
                "0x0000000000000000000000000000000000000000000000400000402000000001"
            };

            var data = "0x";

            var transferDto = new TransferEventMissingIndexedTopic();
            var exception = Assert.Throws<Exception>(() => 
                new EventTopicDecoder().DecodeTopics(transferDto, topics, data)); 
            Assert.Equal($"Number of indexes don't match the number of topics. Indexed Properties 2, Topics : 3", 
                exception.Message);
            
        }
    }
}
