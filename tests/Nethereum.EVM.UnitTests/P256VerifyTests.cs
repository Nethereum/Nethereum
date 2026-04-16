using System;
using System.Security.Cryptography;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Precompiles;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class P256VerifyTests
    {
        private readonly PrecompileRegistry _registry;
        private const int P256_ADDRESS_INT = 256;

        public P256VerifyTests()
        {
            _registry = DefaultPrecompileRegistries.OsakaBase();
        }

        [Fact]
        [Trait("Category", "EIP-7951")]
        public void GasCost_Returns6900()
        {
            var input = new byte[160];
            var gas = _registry.GetGasCost(P256_ADDRESS_INT, input);
            Assert.Equal(GasConstants.P256VERIFY_GAS, gas);
            Assert.Equal(6900, gas);
        }

        [Fact]
        [Trait("Category", "EIP-7951")]
        public void ValidSignature_ReturnsOne()
        {
            var (hash, r, s, x, y) = GenerateValidSignature();
            var input = BuildInput(hash, r, s, x, y);
            var result = _registry.Execute(P256_ADDRESS_INT, input);
            Assert.NotNull(result);
            Assert.Equal(32, result.Length);
            Assert.Equal(1, result[31]);
        }

        [Fact]
        [Trait("Category", "EIP-7951")]
        public void InvalidSignature_WrongHash_ReturnsEmpty()
        {
            var (hash, r, s, x, y) = GenerateValidSignature();
            hash[0] ^= 0xFF;
            var input = BuildInput(hash, r, s, x, y);
            var result = _registry.Execute(P256_ADDRESS_INT, input);
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Theory]
        [Trait("Category", "EIP-7951")]
        [InlineData(0)]
        [InlineData(159)]
        [InlineData(161)]
        public void WrongInputLength_ReturnsEmpty(int length)
        {
            var input = new byte[length];
            var result = _registry.Execute(P256_ADDRESS_INT, input);
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        [Trait("Category", "EIP-7951")]
        public void NullInput_ReturnsEmpty()
        {
            var result = _registry.Execute(P256_ADDRESS_INT, null);
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        [Trait("Category", "EIP-7951")]
        public void PointNotOnCurve_ReturnsEmpty()
        {
            var input = new byte[160];
            input[127] = 1;
            input[159] = 1;
            input[63] = 1;
            input[95] = 1;
            var result = _registry.Execute(P256_ADDRESS_INT, input);
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        [Trait("Category", "EIP-7951")]
        public void PragueConfig_DoesNotIncludeP256Verify()
        {
            var prague = DefaultPrecompileRegistries.PragueBase();
            Assert.False(prague.CanHandle(P256_ADDRESS_INT));
        }

        [Fact]
        [Trait("Category", "EIP-7951")]
        public void OsakaConfig_IncludesP256Verify()
        {
            Assert.True(_registry.CanHandle(P256_ADDRESS_INT));
        }

        [Fact]
        [Trait("Category", "EIP-7951")]
        public void HardforkConfig_OsakaHasCorrectBundles()
        {
            Assert.NotNull(HardforkConfig.Osaka.OpcodeHandlers);
            Assert.NotNull(HardforkConfig.Osaka.TransactionValidationRules);
            Assert.NotNull(HardforkConfig.Osaka.CallFrameInitRules);
        }

        [Fact]
        [Trait("Category", "EIP-7951")]
        public void HardforkConfig_FromName_Osaka()
        {
            var config = Nethereum.EVM.Precompiles.DefaultMainnetHardforkRegistry.Instance.Get(HardforkName.Osaka);
            Assert.NotNull(config.OpcodeHandlers);
        }

        private static (byte[] hash, byte[] r, byte[] s, byte[] x, byte[] y) GenerateValidSignature()
        {
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var message = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(message);
            var sigBytes = ecdsa.SignHash(hash, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            var r = new byte[32];
            var s = new byte[32];
            Array.Copy(sigBytes, 0, r, 0, 32);
            Array.Copy(sigBytes, 32, s, 0, 32);
            var keyParams = ecdsa.ExportParameters(false);
            return (hash, r, s, PadTo32(keyParams.Q.X), PadTo32(keyParams.Q.Y));
        }

        private static byte[] BuildInput(byte[] hash, byte[] r, byte[] s, byte[] x, byte[] y)
        {
            var input = new byte[160];
            Array.Copy(hash, 0, input, 0, 32);
            Array.Copy(r, 0, input, 32, 32);
            Array.Copy(s, 0, input, 64, 32);
            Array.Copy(x, 0, input, 96, 32);
            Array.Copy(y, 0, input, 128, 32);
            return input;
        }

        private static byte[] PadTo32(byte[] data)
        {
            if (data.Length == 32) return data;
            var padded = new byte[32];
            if (data.Length > 32)
                Array.Copy(data, data.Length - 32, padded, 0, 32);
            else
                Array.Copy(data, 0, padded, 32 - data.Length, data.Length);
            return padded;
        }
    }
}
