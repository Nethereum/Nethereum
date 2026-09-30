using System;
using System.Linq;
using System.Numerics;
using Nethereum.AccountAbstraction.ERC7579.Modules.SocialRecovery;
using Nethereum.AccountAbstraction.Signing;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer.Crypto;
using Xunit;
using BouncyBigInteger = Org.BouncyCastle.Math.BigInteger;

namespace Nethereum.AccountAbstraction.UnitTests.Modules.SocialRecovery
{
    public class SocialRecoveryValidatorModuleTests
    {
        private const string ModuleAddress = "0x111111111111111111111111111111111111aaaa";

        [Fact]
        public void ApplySignaturePrefix_PrependsModuleAddress()
        {
            var module = new SocialRecoveryValidatorModule(ModuleAddress, 2);
            var signature = new byte[] { 0xaa, 0xbb, 0xcc };

            var prefixed = module.ApplySignaturePrefix(signature);

            Assert.Equal(20 + signature.Length, prefixed.Length);
            Assert.Equal(ModuleAddress.HexToByteArray(), prefixed[..20]);
            Assert.Equal(signature, prefixed[20..]);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        public void GetEstimationStubSignature_LengthIsThresholdTimes65_EachSlotLowSDummy(int threshold)
        {
            var module = new SocialRecoveryValidatorModule(ModuleAddress, threshold);

            var stub = module.GetEstimationStubSignature();

            Assert.Equal(threshold * 65, stub.Length);

            for (var i = 0; i < threshold; i++)
            {
                var slot = stub.Skip(i * 65).Take(65).ToArray();
                Assert.Equal(65, slot.Length);

                var r = new BouncyBigInteger(1, slot[..32]);
                var s = new BouncyBigInteger(1, slot[32..64]);
                var signature = new ECDSASignature(r, s);

                Assert.True(signature.IsLowS, $"slot {i} estimation dummy signature must be canonical low-S");
            }

            var distinctSlots = Enumerable.Range(0, threshold)
                .Select(i => stub.Skip(i * 65).Take(65).ToArray().ToHex(true))
                .Distinct()
                .Count();
            Assert.Equal(1, distinctSlots);
        }

        [Fact]
        public void GetVerificationGasBuffer_ThresholdOne_IsZero()
        {
            var module = new SocialRecoveryValidatorModule(ModuleAddress, 1);

            Assert.Equal(BigInteger.Zero, module.GetVerificationGasBuffer());
        }

        [Theory]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(5)]
        public void GetVerificationGasBuffer_ScalesLinearlyWithThresholdMinusOne(int threshold)
        {
            var module = new SocialRecoveryValidatorModule(ModuleAddress, threshold);
            var thresholdOneBuffer = new SocialRecoveryValidatorModule(ModuleAddress, 1).GetVerificationGasBuffer();

            var buffer = module.GetVerificationGasBuffer();

            Assert.True(buffer > thresholdOneBuffer, "buffer must strictly increase with threshold");
            Assert.Equal(0, buffer % (threshold - 1));
        }

        [Fact]
        public void Constructor_NullAddress_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SocialRecoveryValidatorModule(null!, 2));
        }

        [Theory]
        [InlineData("")]
        [InlineData("0x1234")]
        [InlineData("not-an-address")]
        public void Constructor_InvalidAddress_Throws(string invalidAddress)
        {
            Assert.Throws<ArgumentException>(() => new SocialRecoveryValidatorModule(invalidAddress, 2));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Constructor_NonPositiveThreshold_Throws(int threshold)
        {
            Assert.Throws<ArgumentException>(() => new SocialRecoveryValidatorModule(ModuleAddress, threshold));
        }
    }
}
