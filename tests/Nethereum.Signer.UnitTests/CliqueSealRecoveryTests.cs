using System;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.UnitTests;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Signer.UnitTests
{
    public class CliqueSealRecoveryTests
    {
        private const int ExtraVanity = 32;
        private const int ExtraSeal = 65;

        private const string SealerPrivateKey =
            "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";

        private static BlockHeader UnsealedCliqueHeader(string shape)
        {
            var header = BlockHeaderShapes.For(shape);
            header.ExtraData = new byte[ExtraVanity + ExtraSeal];
            return header;
        }

        private static BlockHeader Seal(BlockHeader header, EthECKey sealer)
        {
            var sealHash = BlockHeaderEncoder.Current.EncodeCliqueSigHeaderAndHash(header);
            var signature = sealer.SignAndCalculateV(sealHash).CreateStringSignature().HexToByteArray();
            Array.Copy(signature, 0, header.ExtraData, ExtraVanity, ExtraSeal);
            return header;
        }

        private static string Recover(BlockHeader header) =>
            new CliqueBlockHeaderRecovery().RecoverCliqueSigner(header);

        [Fact]
        [Trait("Category", "AMS-HDR-14")]
        public void Given_APragueShapedCliqueHeader_When_TheSealerSignsTheSealHash_Then_TheSealerAddressIsRecovered()
        {
            var sealer = new EthECKey(SealerPrivateKey);
            var header = Seal(UnsealedCliqueHeader("prague"), sealer);

            Assert.True(sealer.GetPublicAddress().IsTheSameAddress(Recover(header)));
        }

        [Fact]
        [Trait("Category", "AMS-HDR-14")]
        public void Given_AnAmsterdamShapedCliqueHeader_When_TheTwoAppendedFieldsArePresent_Then_TheSealerAddressIsStillRecovered()
        {
            var sealer = new EthECKey(SealerPrivateKey);
            var header = Seal(UnsealedCliqueHeader("amsterdam"), sealer);

            Assert.NotNull(header.BlockAccessListHash);
            Assert.NotNull(header.SlotNumber);
            Assert.True(sealer.GetPublicAddress().IsTheSameAddress(Recover(header)));
        }

        [Fact]
        [Trait("Category", "AMS-HDR-14")]
        public void Given_ASealedPragueHeader_When_AFieldOutsideTheSealSuffixIsMutated_Then_ADifferentAddressIsRecovered()
        {
            var sealer = new EthECKey(SealerPrivateKey);
            var header = Seal(UnsealedCliqueHeader("prague"), sealer);

            header.GasUsed += 1;

            Assert.False(sealer.GetPublicAddress().IsTheSameAddress(Recover(header)));
        }

        [Fact]
        [Trait("Category", "AMS-HDR-14")]
        public void Given_ASealedAmsterdamHeader_When_TheBlockAccessListHashIsMutated_Then_ADifferentAddressIsRecovered()
        {
            var sealer = new EthECKey(SealerPrivateKey);
            var header = Seal(UnsealedCliqueHeader("amsterdam"), sealer);

            header.BlockAccessListHash[0] ^= 0xFF;

            Assert.False(sealer.GetPublicAddress().IsTheSameAddress(Recover(header)));
        }

        [Fact]
        [Trait("Category", "AMS-HDR-14")]
        public void Given_ASealedAmsterdamHeader_When_TheSlotNumberIsMutated_Then_ADifferentAddressIsRecovered()
        {
            var sealer = new EthECKey(SealerPrivateKey);
            var header = Seal(UnsealedCliqueHeader("amsterdam"), sealer);

            header.SlotNumber += 1;

            Assert.False(sealer.GetPublicAddress().IsTheSameAddress(Recover(header)));
        }

        [Fact]
        [Trait("Category", "AMS-HDR-14")]
        public void Given_AHeaderSharedWithOtherReaders_When_TheSealHashIsComputed_Then_TheCallersExtraDataIsUnchanged()
        {
            var header = UnsealedCliqueHeader("amsterdam");
            var extraDataBefore = header.ExtraData;

            BlockHeaderEncoder.Current.EncodeCliqueSigHeaderAndHash(header);

            Assert.Same(extraDataBefore, header.ExtraData);
            Assert.Equal(ExtraVanity + ExtraSeal, header.ExtraData.Length);
        }

        [Fact]
        [Trait("Category", "AMS-HDR-14")]
        public void Given_AHeaderEncodedTwiceForItsSealHash_When_NothingElseChanged_Then_BothEncodingsAreIdentical()
        {
            var header = UnsealedCliqueHeader("amsterdam");

            var first = BlockHeaderEncoder.Current.EncodeCliqueSigHeader(header);
            var second = BlockHeaderEncoder.Current.EncodeCliqueSigHeader(header);

            Assert.Equal(first.ToHex(), second.ToHex());
        }

        [Theory]
        [Trait("Category", "AMS-HDR-14")]
        [InlineData(0, 0)]
        [InlineData(32, 0)]
        [InlineData(33, 1)]
        [InlineData(64, 63)]
        [InlineData(65, 0)]
        [InlineData(97, 32)]
        public void Given_ExtraDataAroundTheSealSuffixLength_When_TheSealHeaderIsEncoded_Then_TheSurvivingExtraDataIsMaxOfZeroAndTwiceLengthMinusSuffix(
            int extraDataLength, int expectedSurvivingLength)
        {
            var header = UnsealedCliqueHeader("prague");
            header.ExtraData = Enumerable.Repeat((byte)0xAB, extraDataLength).ToArray();

            var expected = UnsealedCliqueHeader("prague");
            expected.ExtraData = Enumerable.Repeat((byte)0xAB, expectedSurvivingLength).ToArray();

            Assert.Equal(
                BlockHeaderEncoder.Current.Encode(expected).ToHex(),
                BlockHeaderEncoder.Current.EncodeCliqueSigHeader(header).ToHex());
            Assert.Equal(extraDataLength, header.ExtraData.Length);
        }
    }
}
