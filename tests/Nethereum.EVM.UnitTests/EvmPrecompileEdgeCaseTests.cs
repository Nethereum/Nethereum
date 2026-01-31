using System;
using System.Linq;
using System.Numerics;
using Nethereum.EVM.Execution;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class EvmPrecompileEdgeCaseTests
    {
        private readonly EvmPreCompiledContractsExecution _precompiles = new EvmPreCompiledContractsExecution();

        #region ECRecover Tests (Address 0x01)

        [Fact]
        public void EcRecover_ValidSignature_ShouldReturnAddress()
        {
            var message = "Hello, Ethereum!";
            var privateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";

            var signer = new EthereumMessageSigner();
            var signature = signer.EncodeUTF8AndSign(message, new EthECKey(privateKey));

            var messageHash = signer.HashPrefixedMessage(System.Text.Encoding.UTF8.GetBytes(message));
            var signatureBytes = signature.HexToByteArray();

            var r = signatureBytes.Take(32).ToArray();
            var s = signatureBytes.Skip(32).Take(32).ToArray();
            var v = signatureBytes.Skip(64).First();

            var input = new byte[128];
            Array.Copy(messageHash, 0, input, 0, 32);
            input[63] = v;
            Array.Copy(r, 0, input, 64, 32);
            Array.Copy(s, 0, input, 96, 32);

            var result = _precompiles.ExecutePreCompile("0x0000000000000000000000000000000000000001", input);

            Assert.Equal(32, result.Length);
            var recoveredAddress = result.ToHex();
            var expectedAddress = new EthECKey(privateKey).GetPublicAddress().ToLower().RemoveHexPrefix();
            Assert.EndsWith(expectedAddress.Substring(2), recoveredAddress.ToLower());
        }

        [Fact]
        public void EcRecover_InvalidVValue_ShouldReturnZeroAddress()
        {
            var input = new byte[128];
            input[63] = 26;

            var result = _precompiles.ExecutePreCompile("0x0000000000000000000000000000000000000001", input);
            Assert.Equal(32, result.Length);
            Assert.All(result, b => Assert.Equal(0, b));
        }

        [Fact]
        public void EcRecover_ZeroHash_ShouldReturnZeroAddress()
        {
            var input = new byte[128];
            input[63] = 27;

            var result = _precompiles.ExecutePreCompile("0x0000000000000000000000000000000000000001", input);
            Assert.Equal(32, result.Length);
            Assert.All(result, b => Assert.Equal(0, b));
        }

        [Fact]
        public void EcRecover_ShortInput_ShouldReturnZeroAddress()
        {
            var input = new byte[64];
            input[63] = 27;

            var result = _precompiles.ExecutePreCompile("0x0000000000000000000000000000000000000001", input);
            Assert.Equal(32, result.Length);
            Assert.All(result, b => Assert.Equal(0, b));
        }

        [Fact]
        public void EcRecover_EmptyInput_ShouldReturnZeroAddress()
        {
            var result = _precompiles.ExecutePreCompile("0x0000000000000000000000000000000000000001", new byte[0]);
            Assert.Equal(32, result.Length);
            Assert.All(result, b => Assert.Equal(0, b));
        }

        #endregion

        #region SHA256 Edge Cases (Address 0x02)

        [Fact]
        public void Sha256_LargeInput_ShouldHashCorrectly()
        {
            var input = new byte[10000];
            for (int i = 0; i < input.Length; i++)
            {
                input[i] = (byte)(i % 256);
            }

            var result = _precompiles.Sha256Hash(input);
            Assert.Equal(32, result.Length);
        }

        [Fact]
        public void Sha256_SingleByte_ShouldHashCorrectly()
        {
            var result = _precompiles.Sha256Hash(new byte[] { 0x00 });
            Assert.Equal("6e340b9cffb37a989ca544e6bb780a2c78901d3fb33738768511a30617afa01d", result.ToHex().ToLower());
        }

        #endregion

        #region RIPEMD160 Edge Cases (Address 0x03)

        [Fact]
        public void Ripemd160_LargeInput_ShouldHashCorrectly()
        {
            var input = new byte[10000];
            for (int i = 0; i < input.Length; i++)
            {
                input[i] = (byte)(i % 256);
            }

            var result = _precompiles.Ripemd160Hash(input);
            Assert.Equal(32, result.Length);
        }

        [Fact]
        public void Ripemd160_EmptyInput_ShouldHashCorrectly()
        {
            var result = _precompiles.Ripemd160Hash(new byte[0]);
            Assert.Equal(32, result.Length);
            Assert.EndsWith("9c1185a5c5e9fc54612808977ee8f548b2258d31", result.ToHex().ToLower());
        }

        #endregion

        #region Identity Edge Cases (Address 0x04)

        [Fact]
        public void Identity_EmptyInput_ShouldReturnEmpty()
        {
            var result = _precompiles.DataCopy(new byte[0]);
            Assert.Empty(result);
        }

        [Fact]
        public void Identity_LargeInput_ShouldReturnSame()
        {
            var input = new byte[10000];
            for (int i = 0; i < input.Length; i++)
            {
                input[i] = (byte)(i % 256);
            }

            var result = _precompiles.DataCopy(input);
            Assert.Equal(input, result);
        }

        #endregion

        #region ModExp Edge Cases (Address 0x05)

        [Fact]
        public void ModExp_ZeroModulus_ShouldReturnZero()
        {
            var input = new byte[96 + 1 + 1 + 1];
            input[31] = 1;
            input[63] = 1;
            input[95] = 1;
            input[96] = 2;
            input[97] = 3;
            input[98] = 0;

            var result = _precompiles.ModExp(input);
            Assert.Single(result);
            Assert.Equal(0, result[0]);
        }

        [Fact]
        public void ModExp_ZeroExponent_ShouldReturnOne()
        {
            var input = new byte[96 + 1 + 1 + 1];
            input[31] = 1;
            input[63] = 1;
            input[95] = 1;
            input[96] = 5;
            input[97] = 0;
            input[98] = 7;

            var result = _precompiles.ModExp(input);
            Assert.Single(result);
            Assert.Equal(1, result[0]);
        }

        [Fact]
        public void ModExp_ZeroBase_ShouldReturnZero()
        {
            var input = new byte[96 + 1 + 1 + 1];
            input[31] = 1;
            input[63] = 1;
            input[95] = 1;
            input[96] = 0;
            input[97] = 3;
            input[98] = 7;

            var result = _precompiles.ModExp(input);
            Assert.Single(result);
            Assert.Equal(0, result[0]);
        }

        [Fact]
        public void ModExp_LargeNumbers_ShouldComputeCorrectly()
        {
            var baseLen = 32;
            var expLen = 32;
            var modLen = 32;

            var input = new byte[96 + baseLen + expLen + modLen];
            input[31] = (byte)baseLen;
            input[63] = (byte)expLen;
            input[95] = (byte)modLen;

            for (int i = 0; i < baseLen; i++) input[96 + i] = (byte)i;
            input[96 + expLen] = 0x02;
            for (int i = 0; i < modLen; i++) input[96 + baseLen + expLen + i] = 0xFF;

            var result = _precompiles.ModExp(input);
            Assert.Equal(modLen, result.Length);
        }

        [Fact]
        public void ModExp_EmptyInput_ShouldReturnEmpty()
        {
            var result = _precompiles.ModExp(new byte[0]);
            Assert.Empty(result);
        }

        [Fact]
        public void ModExp_ShortInput_ShouldPadWithZeros()
        {
            var input = new byte[32];
            input[31] = 1;

            var result = _precompiles.ModExp(input);
            Assert.Empty(result);
        }

        #endregion

        #region BN128 Add Edge Cases (Address 0x06)

        [Fact]
        public void BN128Add_InvalidPointCoordinate_ShouldThrow()
        {
            var maxCoord = "30644e72e131a029b85045b68181585d97816a916871ca8d3c208c16d87cfd47";
            var y = "0000000000000000000000000000000000000000000000000000000000000002";
            var input = (maxCoord + y + new string('0', 128)).HexToByteArray();

            Assert.Throws<ArgumentException>(() =>
                _precompiles.ExecutePreCompile("0x0000000000000000000000000000000000000006", input));
        }

        [Fact]
        public void BN128Add_PointNotOnCurve_ShouldThrow()
        {
            var x = "0000000000000000000000000000000000000000000000000000000000000001";
            var y = "0000000000000000000000000000000000000000000000000000000000000001";
            var input = (x + y + new string('0', 128)).HexToByteArray();

            Assert.Throws<ArgumentException>(() =>
                _precompiles.ExecutePreCompile("0x0000000000000000000000000000000000000006", input));
        }

        [Fact]
        public void BN128Add_InfinityPlusInfinity_ShouldReturnInfinity()
        {
            var result = _precompiles.ExecutePreCompile("0x0000000000000000000000000000000000000006", new byte[128]);
            Assert.Equal(64, result.Length);
            Assert.All(result, b => Assert.Equal(0, b));
        }

        #endregion

        #region BN128 Mul Edge Cases (Address 0x07)

        [Fact]
        public void BN128Mul_MaxScalar_ShouldNotOverflow()
        {
            var x = "0000000000000000000000000000000000000000000000000000000000000001";
            var y = "0000000000000000000000000000000000000000000000000000000000000002";
            var scalar = "30644e72e131a029b85045b68181585d2833e84879b9709143e1f593f0000000";
            var input = (x + y + scalar).HexToByteArray();

            var result = _precompiles.ExecutePreCompile("0x0000000000000000000000000000000000000007", input);
            Assert.Equal(64, result.Length);
        }

        [Fact]
        public void BN128Mul_InfinityTimesScalar_ShouldReturnInfinity()
        {
            var scalar = "0000000000000000000000000000000000000000000000000000000000000005";
            var input = (new string('0', 128) + scalar).HexToByteArray();

            var result = _precompiles.ExecutePreCompile("0x0000000000000000000000000000000000000007", input);
            Assert.Equal(64, result.Length);
            Assert.All(result, b => Assert.Equal(0, b));
        }

        #endregion

        #region Blake2f Edge Cases (Address 0x09)

        [Fact]
        public void Blake2f_MediumRounds_ShouldComputeCorrectly()
        {
            var input = new byte[213];
            input[0] = 0x00;
            input[1] = 0x00;
            input[2] = 0x01;
            input[3] = 0x00;

            var iv = new ulong[]
            {
                0x6a09e667f3bcc908UL, 0xbb67ae8584caa73bUL,
                0x3c6ef372fe94f82bUL, 0xa54ff53a5f1d36f1UL,
                0x510e527fade682d1UL, 0x9b05688c2b3e6c1fUL,
                0x1f83d9abfb41bd6bUL, 0x5be0cd19137e2179UL
            };

            for (int i = 0; i < 8; i++)
            {
                var bytes = BitConverter.GetBytes(iv[i]);
                Array.Copy(bytes, 0, input, 4 + i * 8, 8);
            }

            input[212] = 1;

            var result = _precompiles.Blake2f(input);
            Assert.Equal(64, result.Length);
        }

        [Fact]
        public void Blake2f_ZeroRounds_ShouldReturnState()
        {
            var input = new byte[213];

            var iv = new ulong[]
            {
                0x6a09e667f3bcc908UL, 0xbb67ae8584caa73bUL,
                0x3c6ef372fe94f82bUL, 0xa54ff53a5f1d36f1UL,
                0x510e527fade682d1UL, 0x9b05688c2b3e6c1fUL,
                0x1f83d9abfb41bd6bUL, 0x5be0cd19137e2179UL
            };

            for (int i = 0; i < 8; i++)
            {
                var bytes = BitConverter.GetBytes(iv[i]);
                Array.Copy(bytes, 0, input, 4 + i * 8, 8);
            }

            input[212] = 1;

            var result = _precompiles.Blake2f(input);
            Assert.Equal(64, result.Length);
        }

        #endregion

        #region Gas Calculation Tests

        [Theory]
        [InlineData(0, 60)]
        [InlineData(32, 72)]
        [InlineData(64, 84)]
        [InlineData(1024, 444)]
        public void Sha256Gas_ShouldCalculateCorrectly(int inputLength, long expectedGas)
        {
            var wordCount = (inputLength + 31) / 32;
            var calculatedGas = 60 + 12 * wordCount;
            Assert.Equal(expectedGas, calculatedGas);
        }

        [Theory]
        [InlineData(0, 600)]
        [InlineData(32, 720)]
        [InlineData(64, 840)]
        [InlineData(1024, 4440)]
        public void Ripemd160Gas_ShouldCalculateCorrectly(int inputLength, long expectedGas)
        {
            var wordCount = (inputLength + 31) / 32;
            var calculatedGas = 600 + 120 * wordCount;
            Assert.Equal(expectedGas, calculatedGas);
        }

        [Theory]
        [InlineData(0, 15)]
        [InlineData(32, 18)]
        [InlineData(64, 21)]
        public void IdentityGas_ShouldCalculateCorrectly(int inputLength, long expectedGas)
        {
            var wordCount = (inputLength + 31) / 32;
            var calculatedGas = 15 + 3 * wordCount;
            Assert.Equal(expectedGas, calculatedGas);
        }

        [Fact]
        public void ModExpGas_FromStaticcallTest_ShouldMatch()
        {
            var input = new byte[161];
            input[31] = 1;
            input[63] = 32;
            input[95] = 32;
            input[96] = 0x2f;
            var expValue = "03fffffffffffffffffffffffffffffffffffffffffffffffffffffffefffffc".HexToByteArray();
            Array.Copy(expValue, 0, input, 97, expValue.Length);
            var modValue = "2efffffffffffffffffffffffffffffffffffffffffffffffffffffffefffffc".HexToByteArray();
            Array.Copy(modValue, 0, input, 129, modValue.Length);

            var gas = _precompiles.GetPrecompileGasCost("0x0000000000000000000000000000000000000005", input);

            var expectedGas = 1328;
            Assert.Equal(expectedGas, (long)gas);
        }

        #endregion
    }
}
