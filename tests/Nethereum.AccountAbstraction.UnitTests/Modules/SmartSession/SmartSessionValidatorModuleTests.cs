using System;
using System.Numerics;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer.Crypto;
using Xunit;
using BouncyBigInteger = Org.BouncyCastle.Math.BigInteger;

namespace Nethereum.AccountAbstraction.UnitTests.Modules.SmartSession
{
    public class SmartSessionValidatorModuleTests
    {
        private const string ModuleAddress = "0x111111111111111111111111111111111111aaaa";

        private static readonly byte[] PermissionId =
            "0x0102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f20".HexToByteArray();

        [Fact]
        public void ApplySignaturePrefix_PrependsModuleAddress()
        {
            var module = new SmartSessionValidatorModule(ModuleAddress, PermissionId);
            var signature = new byte[] { 0xaa, 0xbb, 0xcc };

            var prefixed = module.ApplySignaturePrefix(signature);

            Assert.Equal(20 + signature.Length, prefixed.Length);
            Assert.Equal(ModuleAddress.HexToByteArray(), prefixed[..20]);
            Assert.Equal(signature, prefixed[20..]);
        }

        [Fact]
        public void GetEstimationStubSignature_IsModeThenPermissionIdThenLowSDummySignature()
        {
            var module = new SmartSessionValidatorModule(ModuleAddress, PermissionId);

            var stub = module.GetEstimationStubSignature();

            Assert.Equal(1 + 32 + 65, stub.Length);
            Assert.Equal(0x00, stub[0]);
            Assert.Equal(PermissionId, stub[1..33]);

            var dummySignature = stub[33..98];
            Assert.Equal(65, dummySignature.Length);

            var r = new BouncyBigInteger(1, dummySignature[..32]);
            var s = new BouncyBigInteger(1, dummySignature[32..64]);
            var signature = new ECDSASignature(r, s);

            Assert.True(signature.IsLowS, "Estimation dummy signature must be canonical low-S");
        }

        [Fact]
        public void GetVerificationGasBuffer_IsZero()
        {
            var module = new SmartSessionValidatorModule(ModuleAddress, PermissionId);

            Assert.Equal(System.Numerics.BigInteger.Zero, module.GetVerificationGasBuffer());
        }

        [Theory]
        [InlineData(31)]
        [InlineData(33)]
        public void Constructor_WrongLengthPermissionId_Throws(int length)
        {
            var badPermissionId = new byte[length];

            var ex = Assert.Throws<ArgumentException>(
                () => new SmartSessionValidatorModule(ModuleAddress, badPermissionId));

            Assert.Contains(length.ToString(), ex.Message);
        }

        [Fact]
        public void Constructor_NullPermissionId_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new SmartSessionValidatorModule(ModuleAddress, null));
        }

        [Fact]
        public void Constructor_NullAddress_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SmartSessionValidatorModule(null, PermissionId));
        }

        [Theory]
        [InlineData("")]
        [InlineData("0x1234")]
        [InlineData("not-an-address")]
        public void Constructor_InvalidAddress_Throws(string invalidAddress)
        {
            Assert.Throws<ArgumentException>(() => new SmartSessionValidatorModule(invalidAddress, PermissionId));
        }
    }
}
