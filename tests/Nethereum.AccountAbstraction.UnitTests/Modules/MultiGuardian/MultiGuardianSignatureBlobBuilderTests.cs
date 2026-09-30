using System;
using System.Linq;
using Nethereum.AccountAbstraction.ERC7579.Modules.MultiGuardian;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Modules.MultiGuardian
{
    public class MultiGuardianSignatureBlobBuilderTests
    {
        private static readonly byte[] UserOpHash =
            "0x1122334455667788990011223344556677889900112233445566778899aabb00".HexToByteArray();

        private static EthECKey[] GenerateGuardians(int count) =>
            Enumerable.Range(0, count).Select(_ => EthECKey.GenerateKey()).ToArray();

        [Fact]
        public void BuildFromUserOpHash_BlobLength_IsExactlyThresholdTimes65()
        {
            var guardians = GenerateGuardians(3);

            var blob = MultiGuardianSignatureBlobBuilder.BuildFromUserOpHash(UserOpHash, guardians, 2);

            Assert.Equal(2 * 65, blob.Length);
        }

        [Fact]
        public void BuildFromUserOpHash_EachSlot_IsRSVOrderAndRecoversToItsGuardian()
        {
            var guardians = GenerateGuardians(3);
            var threshold = 3;

            var blob = MultiGuardianSignatureBlobBuilder.BuildFromUserOpHash(UserOpHash, guardians, threshold);

            var expectedOrder = guardians
                .Select(g => g.GetPublicAddress())
                .OrderBy(a => a.HexToBigInteger(false))
                .ToArray();

            var messageSigner = new EthereumMessageSigner();
            for (var i = 0; i < threshold; i++)
            {
                var slot = blob.Skip(i * 65).Take(65).ToArray();
                Assert.Equal(65, slot.Length);

                var v = slot[64];
                Assert.True(v == 27 || v == 28, $"slot {i} v must be standard 27/28 (was {v})");

                var recovered = messageSigner.EcRecover(UserOpHash, slot.ToHex(true));
                Assert.Equal(expectedOrder[i], recovered, ignoreCase: true);
            }
        }

        [Fact]
        public void BuildFromUserOpHash_ExtraGuardiansBeyondThreshold_AreNotIncluded()
        {
            var guardians = GenerateGuardians(5);
            var threshold = 2;

            var blob = MultiGuardianSignatureBlobBuilder.BuildFromUserOpHash(UserOpHash, guardians, threshold);

            Assert.Equal(threshold * 65, blob.Length);

            var messageSigner = new EthereumMessageSigner();
            var recoveredAddresses = Enumerable.Range(0, threshold)
                .Select(i => messageSigner.EcRecover(UserOpHash, blob.Skip(i * 65).Take(65).ToArray().ToHex(true)))
                .ToArray();

            var participatingAddresses = guardians.Take(threshold).Select(g => g.GetPublicAddress());
            foreach (var address in recoveredAddresses)
                Assert.Contains(participatingAddresses, a => a.IsTheSameAddress(address));

            var excludedAddress = guardians[threshold].GetPublicAddress();
            Assert.DoesNotContain(recoveredAddresses, a => excludedAddress.IsTheSameAddress(a));
        }

        [Fact]
        public void BuildFromFinalHash_SignsTheGivenHashDirectly_NoAdditionalWrap()
        {
            var guardian = GenerateGuardians(1)[0];
            var finalHash = UserOpHash;

            var blob = MultiGuardianSignatureBlobBuilder.BuildFromFinalHash(finalHash, new[] { guardian }, 1);

            var recovered = new MessageSigner().EcRecover(finalHash, blob.ToHex(true));
            Assert.Equal(guardian.GetPublicAddress(), recovered, ignoreCase: true);
        }

        [Fact]
        public void BuildFromUserOpHash_FewerGuardiansThanThreshold_Throws()
        {
            var guardians = GenerateGuardians(2);

            Assert.Throws<ArgumentException>(
                () => MultiGuardianSignatureBlobBuilder.BuildFromUserOpHash(UserOpHash, guardians, 3));
        }

        [Fact]
        public void BuildFromUserOpHash_NullUserOpHash_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => MultiGuardianSignatureBlobBuilder.BuildFromUserOpHash(null!, GenerateGuardians(1), 1));
        }

        [Theory]
        [InlineData(31)]
        [InlineData(33)]
        public void BuildFromUserOpHash_WrongLengthUserOpHash_Throws(int length)
        {
            var badHash = new byte[length];

            Assert.Throws<ArgumentException>(
                () => MultiGuardianSignatureBlobBuilder.BuildFromUserOpHash(badHash, GenerateGuardians(1), 1));
        }

        [Fact]
        public void BuildFromUserOpHash_NullGuardians_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => MultiGuardianSignatureBlobBuilder.BuildFromUserOpHash(UserOpHash, null!, 1));
        }

        [Fact]
        public void BuildFromUserOpHash_GuardiansContainingNull_Throws()
        {
            var guardians = new[] { EthECKey.GenerateKey(), null! };

            Assert.Throws<ArgumentException>(
                () => MultiGuardianSignatureBlobBuilder.BuildFromUserOpHash(UserOpHash, guardians, 1));
        }

        [Fact]
        public void BuildFromUserOpHash_ZeroThreshold_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => MultiGuardianSignatureBlobBuilder.BuildFromUserOpHash(UserOpHash, GenerateGuardians(2), 0));
        }

        [Fact]
        public void BuildFromUserOpHash_ThresholdGreaterThanGuardianCount_Throws()
        {
            Assert.Throws<ArgumentException>(
                () => MultiGuardianSignatureBlobBuilder.BuildFromUserOpHash(UserOpHash, GenerateGuardians(2), 3));
        }

        [Fact]
        public void BuildFromUserOpHash_DuplicateGuardianAddresses_Throws()
        {
            var guardian = EthECKey.GenerateKey();
            var guardians = new[] { guardian, guardian };

            Assert.Throws<ArgumentException>(
                () => MultiGuardianSignatureBlobBuilder.BuildFromUserOpHash(UserOpHash, guardians, 2));
        }
    }
}
