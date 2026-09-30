using System;
using System.Linq;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.SSZ;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests
{
    public class BlockHashCalculatorProviderRoutingTests
    {
        private static byte[] Bytes(byte fill, int length = 32)
            => Enumerable.Repeat(fill, length).ToArray();

        private static BlockHeader PragueShaped()
            => new BlockHeader
            {
                ParentHash = Bytes(0x11),
                UnclesHash = Bytes(0x22),
                Coinbase = "0x3333333333333333333333333333333333333333",
                StateRoot = Bytes(0x44),
                TransactionsHash = Bytes(0x55),
                ReceiptHash = Bytes(0x66),
                LogsBloom = Bytes(0x77, 256),
                Difficulty = 0,
                BlockNumber = 1234,
                GasLimit = 30_000_000,
                GasUsed = 21_000,
                Timestamp = 1_700_000_000,
                ExtraData = Bytes(0x88, 4),
                MixHash = Bytes(0x99),
                Nonce = Bytes(0xaa, 8),
                BaseFee = 1_000_000_000,
                WithdrawalsRoot = Bytes(0xbb),
                BlobGasUsed = 0,
                ExcessBlobGas = 0,
                ParentBeaconBlockRoot = Bytes(0xcc),
                RequestsHash = Bytes(0xdd)
            };

        private static BlockHeader AmsterdamShaped()
        {
            var header = PragueShaped();
            header.BlockAccessListHash = Bytes(0xee);
            header.SlotNumber = 1234;
            return header;
        }

        [Fact]
        public void Given_ABlockHashProviderThatUsesTheShapeSelector_When_TheForkRouteIsAskedInstead_Then_ItRefusesAHeaderOutOfShape()
        {
            var missingItsAmsterdamFields = PragueShaped();

            var byShape = RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(missingItsAmsterdamFields);
            Assert.Equal(32, byShape.Length);

            Assert.Throws<ArgumentException>(() => BlockHashCalculator.ForFork(
                missingItsAmsterdamFields, HardforkName.Amsterdam, RlpKeccakBlockHashProvider.Instance));
        }

        [Fact]
        public void Given_AnAmsterdamShapedHeader_When_HashedByForkAndByShape_Then_BothRoutesAgree()
        {
            var header = AmsterdamShaped();

            Assert.Equal(
                RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header).ToHex(true),
                BlockHashCalculator.ForFork(
                    header, HardforkName.Amsterdam, RlpKeccakBlockHashProvider.Instance).ToHex(true));
        }

        [Fact]
        public void Given_AProviderThatDefinesItsOwnHash_When_TheProducerHashesForAFork_Then_TheForkDoesNotEnterTheAnswer()
        {
            var header = AmsterdamShaped();

            var routed = BlockHashCalculator.ForFork(
                header, HardforkName.Amsterdam, SszSha256BlockHashProvider.Instance);

            Assert.Equal(SszSha256BlockHashProvider.Instance.ComputeBlockHash(header).ToHex(true), routed.ToHex(true));
            Assert.NotEqual(RlpKeccakBlockHashProvider.Instance.ComputeBlockHash(header).ToHex(true), routed.ToHex(true));
        }
    }
}
