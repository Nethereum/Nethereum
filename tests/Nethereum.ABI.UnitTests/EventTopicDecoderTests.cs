using System;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Util;
using Xunit;

namespace Nethereum.ABI.UnitTests
{
    public class EventTopicDecoderTests
    {
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

            Assert.True("0x0000000000000000000000000000000000000000".IsTheSameAddress(transferDto.From));
            Assert.True("0xc14934679e71ef4d18b6ae927fe2b953c7fd9b91".IsTheSameAddress(transferDto.To));
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
            Assert.Equal("Number of indexes don't match the number of topics. Indexed Properties 2, Topics : 3",
                exception.Message);
        }

		// original event here: https://etherscan.io/tx/0xfd574dd63703b782663abadf7b3a268f5e7d2e6ba1a268d907788fbec09b2963#eventlog#300
		[Fact]
        public void DecodeTopics_Decodes_UniV3Swap_Event()
        {
            String[] topics = [
					"0xc42079f94a6350d7e6235f29174924f928cc2ac818eb64fed8004e115fbcca67",
					"0x000000000000000000000000a88bd9c71ef8bdb60105e4d5de2d4fd71be2d3b2",
					"0x000000000000000000000000e45b4a84e0ad24b8617a489d743c52b84b7acebe"
				];

            var data = "0x" + String.Concat(
					"fffffffffffffffffffffffffffffffffffffffffffffffffd7532715ed0d8eb0000000000000000",
					"000000000000000000000000000000000000000015b3046000000000000000000000000000000000",
					"000000000002eacd7ddd5d098d7ae0f8000000000000000000000000000000000000000000000000",
					"3e24dad3c998033efffffffffffffffffffffffffffffffffffffffffffffffffffffffffffcf12b"
				);

            var swapDto = new UniV3SwapEvent();
            new EventTopicDecoder().DecodeTopics(swapDto, topics, data);

			Assert.True("0xa88Bd9c71EF8bDB60105E4d5dE2D4FD71BE2d3B2".IsTheSameAddress(swapDto.Sender));
			Assert.True("0xe45b4a84E0aD24B8617a489d743c52B84B7aCeBE".IsTheSameAddress(swapDto.Recipient));
			Assert.Equal(-183184747335198485, swapDto.Amount0);
            Assert.Equal(364053600, swapDto.Amount1);
            Assert.Equal("3526676048263385991274744", swapDto.SqrtPriceX96.ToString());
            Assert.Equal((UInt128)4477944532668252990, swapDto.Liquidity);
            Assert.Equal(-200405, swapDto.Tick);
		}

		[Event("Transfer")]
        public class TransferEvent
        {
            [Parameter("address", "_from", 1, true)]
            public string From { get; set; }

            [Parameter("address", "_to", 2, true)] public string To { get; set; }

            [Parameter("uint256", "_value", 3, true)]
            public BigInteger Value { get; set; }
        }

        [Event("Transfer")]
        public class TransferEventMissingIndexedTopic
        {
            [Parameter("address", "_from", 1, true)]
            public string From { get; set; }

            [Parameter("address", "_to", 2, true)] public string To { get; set; }

            [Parameter("uint256", "_value", 3, false)]
            public BigInteger Value { get; set; }
        }

        [Event("Swap")]
        public class UniV3SwapEvent
        {
            [Parameter("address", "sender", 1, true)]
            public string Sender { get; set; }
            [Parameter("address", "recipient", 2, true)]
            public string Recipient { get; set; }
            [Parameter("int256", "amount0", 3, false)]
            public BigInteger Amount0 { get; set; }
            [Parameter("int256", "amount1", 4, false)]
            public BigInteger Amount1 { get; set; }
            [Parameter("uint160", "sqrtPriceX96", 5, false)]
            public BigInteger SqrtPriceX96 { get; set; }
            [Parameter("uint128", "liquidity", 6, false)]
            public UInt128 Liquidity { get; set; }
            [Parameter("int24", "tick", 7, false)]
            public int Tick { get; set; }
		}
    }
}